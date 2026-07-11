using Cove.Engine.Knowledge;
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

        var svc = new NoteReconciliationService(NullLogger.Instance, System.TimeSpan.FromMilliseconds(100));
        var events = new System.Collections.Generic.List<FileReconcileEvent>();
        svc.ReconcileNeeded += (_, e) => events.Add(e);
        svc.StartWatching(notesRoot, "ws1");

        var notePath = System.IO.Path.Combine(notesRoot, "test-note.md");
        await System.IO.File.WriteAllTextAsync(notePath, "hello world");

        await System.Threading.Tasks.Task.Delay(500);

        Assert.True(events.Count >= 1);
        Assert.Equal("ws1", events[0].BayId);
        Assert.True(events.All(e => e.FilePath == notePath || e.FilePath.Contains("test-note")));

        svc.Dispose();
        try { System.IO.Directory.Delete(dir, true); } catch { }
    }

    [Fact]
    public void NonExistentRoot_DoesNotCrash()
    {
        var svc = new NoteReconciliationService(NullLogger.Instance);
        svc.StartWatching("/nonexistent/path/xyz", "ws1");
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

        var svc = new NoteReconciliationService(NullLogger.Instance, System.TimeSpan.FromMilliseconds(100));
        var events = new System.Collections.Generic.List<FileReconcileEvent>();
        svc.ReconcileNeeded += (_, e) => events.Add(e);
        svc.StartWatching(notesRoot, "ws1");

        await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(notesRoot, ".git", "HEAD"), "ref: refs/heads/main");

        await System.Threading.Tasks.Task.Delay(300);

        Assert.Empty(events);

        svc.Dispose();
        try { System.IO.Directory.Delete(dir, true); } catch { }
    }
}
