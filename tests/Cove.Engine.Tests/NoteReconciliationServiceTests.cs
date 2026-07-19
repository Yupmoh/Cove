using Cove.Engine.Knowledge;
using Cove.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class NoteReconciliationServiceTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-reconcile-" + System.Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task FileEdit_FiresExactlyOneDebouncedReconcileEvent()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        var notesRoot = System.IO.Path.Combine(dir, "notes", "ws1");
        System.IO.Directory.CreateDirectory(notesRoot);

        using var svc = new NoteReconciliationService(NullLogger.Instance, System.TimeSpan.FromMilliseconds(100));
        var events = new System.Collections.Generic.List<FileReconcileEvent>();
        var reconciled = new TaskCompletionSource<FileReconcileEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        svc.ReconcileNeeded += (_, e) =>
        {
            events.Add(e);
            reconciled.TrySetResult(e);
        };
        svc.StartWatching(notesRoot, "ws1");

        var notePath = System.IO.Path.Combine(notesRoot, "test-note.md");
        await System.IO.File.WriteAllTextAsync(notePath, "hello world");

        var first = await AsyncTest.CompletesWithinAsync(
            reconciled.Task,
            TimeSpan.FromSeconds(5),
            "file watcher did not reconcile the note");
        svc.StopWatching();

        Assert.Equal("ws1", first.BayId);
        Assert.Equal(notePath, first.FilePath);
        Assert.Single(events);

        Cove.Testing.TestDirectory.Delete(dir);
    }

    [Fact]
    public void NonExistentRoot_DoesNotCrash()
    {
        using var svc = new NoteReconciliationService(NullLogger.Instance);
        svc.StartWatching("/nonexistent/path/xyz", "ws1");
    }

    [Fact]
    public void StopWatchingAndDispose_AreIdempotent()
    {
        var svc = new NoteReconciliationService(NullLogger.Instance);

        svc.StopWatching();
        svc.StopWatching();
        svc.Dispose();
        svc.StopWatching();
        svc.Dispose();
    }

    [Fact]
    public async Task GitFiles_AreIgnored()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        var notesRoot = System.IO.Path.Combine(dir, "notes", "ws1");
        System.IO.Directory.CreateDirectory(notesRoot);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(notesRoot, ".git"));

        using var svc = new NoteReconciliationService(NullLogger.Instance, System.TimeSpan.FromMilliseconds(100));
        var events = new System.Collections.Generic.List<FileReconcileEvent>();
        var barrier = new TaskCompletionSource<FileReconcileEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        svc.ReconcileNeeded += (_, e) =>
        {
            events.Add(e);
            if (e.FilePath.EndsWith("barrier.md", StringComparison.Ordinal))
                barrier.TrySetResult(e);
        };
        svc.StartWatching(notesRoot, "ws1");

        await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(notesRoot, ".git", "HEAD"), "ref: refs/heads/main");
        await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(notesRoot, "barrier.md"), "barrier");

        await AsyncTest.CompletesWithinAsync(
            barrier.Task,
            TimeSpan.FromSeconds(5),
            "file watcher did not reach the barrier file");
        svc.StopWatching();

        Assert.DoesNotContain(events, e => e.FilePath.Contains(".git", StringComparison.Ordinal));

        Cove.Testing.TestDirectory.Delete(dir);
    }

    [Fact]
    public async Task Dispose_WaitsForEnteredCallback_AndPreventsLaterNotification()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        var notesRoot = System.IO.Path.Combine(dir, "notes", "ws1");
        System.IO.Directory.CreateDirectory(notesRoot);

        var svc = new NoteReconciliationService(NullLogger.Instance, System.TimeSpan.FromMilliseconds(50));
        using var callbackEntered = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();
        using var disposeStarted = new ManualResetEventSlim();
        using var disposeReturned = new ManualResetEventSlim();
        var callbackFailure = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var unexpectedNotification = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Exception? disposeFailure = null;
        var notificationCount = 0;
        svc.ReconcileNeeded += (_, _) =>
        {
            if (Interlocked.Increment(ref notificationCount) != 1)
            {
                unexpectedNotification.TrySetResult();
                return;
            }

            callbackEntered.Set();
            if (!releaseCallback.Wait(TimeSpan.FromSeconds(5)))
                callbackFailure.TrySetException(new TimeoutException("test did not release the parked reconcile callback"));
        };

        try
        {
            svc.StartWatching(notesRoot, "ws1");
            await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(notesRoot, "first.md"), "first");
            Assert.True(
                callbackEntered.Wait(TimeSpan.FromSeconds(5)),
                "file watcher callback did not enter before the deadline");

            var disposeThread = new Thread(() =>
            {
                disposeStarted.Set();
                try
                {
                    svc.Dispose();
                }
                catch (Exception exception)
                {
                    disposeFailure = exception;
                }
                finally
                {
                    disposeReturned.Set();
                }
            });
            disposeThread.Start();
            Assert.True(
                disposeStarted.Wait(TimeSpan.FromSeconds(5)),
                "dispose thread did not start before the deadline");
            await AsyncTest.EventuallyAsync(
                () => disposeReturned.IsSet || (disposeThread.ThreadState & ThreadState.WaitSleepJoin) != 0,
                TimeSpan.FromSeconds(5),
                "Dispose neither returned nor blocked behind the entered callback");
            Assert.False(disposeReturned.IsSet);

            releaseCallback.Set();
            Assert.True(
                disposeReturned.Wait(TimeSpan.FromSeconds(5)),
                "Dispose did not return after the reconcile callback was released");
            Assert.True(
                disposeThread.Join(TimeSpan.FromSeconds(1)),
                "dispose thread remained alive after reporting completion");
            Assert.Null(disposeFailure);
            Assert.False(callbackFailure.Task.IsCompleted);

            await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(notesRoot, "after-dispose.md"), "after");
            var winner = await Task.WhenAny(unexpectedNotification.Task, Task.Delay(TimeSpan.FromMilliseconds(250)));
            Assert.NotSame(unexpectedNotification.Task, winner);
            Assert.Equal(1, Volatile.Read(ref notificationCount));
        }
        finally
        {
            releaseCallback.Set();
            svc.Dispose();
            Cove.Testing.TestDirectory.Delete(dir);
        }
    }

    [Fact]
    public async Task ReconcileHandler_CanDisposeWithoutDeadlock()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        var notesRoot = System.IO.Path.Combine(dir, "notes", "ws1");
        System.IO.Directory.CreateDirectory(notesRoot);

        var svc = new NoteReconciliationService(NullLogger.Instance, System.TimeSpan.FromMilliseconds(50));
        var disposeReturned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var unexpectedNotification = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var notificationCount = 0;
        svc.ReconcileNeeded += (_, _) =>
        {
            if (Interlocked.Increment(ref notificationCount) != 1)
            {
                unexpectedNotification.TrySetResult();
                return;
            }

            svc.Dispose();
            disposeReturned.TrySetResult();
        };
        svc.ReconcileNeeded += (_, _) => unexpectedNotification.TrySetResult();

        try
        {
            svc.StartWatching(notesRoot, "ws1");
            await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(notesRoot, "first.md"), "first");
            await AsyncTest.CompletesWithinAsync(
                disposeReturned.Task,
                TimeSpan.FromSeconds(5),
                "reentrant Dispose deadlocked the reconcile callback");

            await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(notesRoot, "after-dispose.md"), "after");
            var winner = await Task.WhenAny(unexpectedNotification.Task, Task.Delay(TimeSpan.FromMilliseconds(250)));
            Assert.NotSame(unexpectedNotification.Task, winner);
            Assert.Equal(1, Volatile.Read(ref notificationCount));

            svc.StopWatching();
            svc.Dispose();
        }
        finally
        {
            svc.Dispose();
            Cove.Testing.TestDirectory.Delete(dir);
        }
    }
}
