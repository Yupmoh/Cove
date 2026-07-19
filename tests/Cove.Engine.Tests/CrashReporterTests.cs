using Cove.Engine.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class CrashReporterTests
{
    private static string NewDir()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-crash-{System.Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void RecordCrash_PersistsDump()
    {
        var dir = NewDir();
        var reporter = new CrashReporter(dir, NullLogger.Instance);
        var dump = reporter.RecordCrash("cove-daemon", "NullReferenceException", "Object reference not set", "at Foo() in Bar.cs:line 42");

        Assert.False(string.IsNullOrEmpty(dump.Id));
        Assert.Equal("cove-daemon", dump.ProcessName);
        Assert.Equal("NullReferenceException", dump.ExceptionType);
        Assert.True(System.IO.File.Exists(dump.FilePath));
    }

    [Fact]
    public void ListCrashes_ReturnsAllDumps()
    {
        var dir = NewDir();
        var reporter = new CrashReporter(dir, NullLogger.Instance);
        reporter.RecordCrash("proc1", "Exception1", "msg1", null);
        reporter.RecordCrash("proc2", "Exception2", "msg2", null);

        var crashes = reporter.ListCrashes();
        Assert.Equal(2, crashes.Count);
    }

    [Fact]
    public void ListCrashes_ReturnsMostRecentFirst()
    {
        var dir = NewDir();
        var time = new ManualTimeProvider();
        var reporter = new CrashReporter(dir, NullLogger.Instance, time);
        reporter.RecordCrash("first", "Ex1", "msg1", null);
        time.Advance(TimeSpan.FromMilliseconds(1));
        reporter.RecordCrash("second", "Ex2", "msg2", null);

        var crashes = reporter.ListCrashes();
        Assert.Equal("second", crashes[0].ProcessName);
        Assert.Equal("first", crashes[1].ProcessName);
    }

    [Fact]
    public void DeleteCrash_RemovesDump()
    {
        var dir = NewDir();
        var reporter = new CrashReporter(dir, NullLogger.Instance);
        var dump = reporter.RecordCrash("proc", "Ex", "msg", null);

        Assert.True(reporter.DeleteCrash(dump.Id));
        Assert.DoesNotContain(reporter.ListCrashes(), c => c.Id == dump.Id);
    }

    [Fact]
    public void DeleteCrash_Nonexistent_ReturnsFalse()
    {
        var reporter = new CrashReporter(NewDir(), NullLogger.Instance);
        Assert.False(reporter.DeleteCrash("nonexistent"));
    }

    [Fact]
    public void Prune_RemovesOldDumps()
    {
        var dir = NewDir();
        try
        {
            var time = new ManualTimeProvider();
            var reporter = new CrashReporter(dir, NullLogger.Instance, time);
            var dump = reporter.RecordCrash("old", "Ex", "msg", null);

            time.Advance(TimeSpan.FromDays(8));

            reporter.Prune();
            Assert.False(System.IO.File.Exists(dump.FilePath));
        }
        finally
        {
            System.IO.Directory.Delete(dir, recursive: true);
        }

        Assert.False(System.IO.Directory.Exists(dir));
    }

    [Fact]
    public void Prune_RemovesOldCorruptDumpsUsingLastWriteTime()
    {
        var now = new System.DateTimeOffset(2026, 1, 20, 12, 0, 0, System.TimeSpan.Zero);
        var dir = NewDir();
        try
        {
            var time = new ManualTimeProvider(now);
            var reporter = new CrashReporter(dir, NullLogger.Instance, time);
            var valid = reporter.RecordCrash("valid", "Ex", "msg", null);
            var corrupt = System.IO.Path.Combine(dir, "diagnostics", "crash-corrupt.json");
            System.IO.File.WriteAllText(corrupt, "{not-json");
            System.IO.File.SetLastWriteTimeUtc(valid.FilePath, now.AddDays(-30).UtcDateTime);
            System.IO.File.SetLastWriteTimeUtc(corrupt, now.AddDays(-8).UtcDateTime);

            reporter.Prune();

            Assert.True(System.IO.File.Exists(valid.FilePath));
            Assert.False(System.IO.File.Exists(corrupt));
        }
        finally
        {
            System.IO.Directory.Delete(dir, recursive: true);
        }

        Assert.False(System.IO.Directory.Exists(dir));
    }

    [Fact]
    public void Prune_EnforcesSizeCap()
    {
        var dir = NewDir();
        try
        {
            var reporter = new CrashReporter(dir, NullLogger.Instance);
            for (var i = 0; i < 5; i++)
            {
                reporter.RecordCrash("proc", "Ex", new string('x', 3 * 1024 * 1024), null);
            }

            reporter.Prune();
            var files = System.IO.Directory.EnumerateFiles(System.IO.Path.Combine(dir, "diagnostics"), "crash-*.json");
            var totalSize = files.Sum(f => new System.IO.FileInfo(f).Length);
            Assert.True(totalSize <= 10 * 1024 * 1024 + 1024);
        }
        finally
        {
            System.IO.Directory.Delete(dir, recursive: true);
        }

        Assert.False(System.IO.Directory.Exists(dir));
    }

    [Fact]
    public void Prune_CountsCorruptBytesAndOrdersValidDumpsByPersistedTime()
    {
        var firstAt = new System.DateTimeOffset(2026, 1, 13, 12, 0, 0, System.TimeSpan.Zero);
        var dir = NewDir();
        try
        {
            var time = new ManualTimeProvider(firstAt);
            var reporter = new CrashReporter(dir, NullLogger.Instance, time);
            var olderValid = reporter.RecordCrash("older", "Ex", new string('x', 4 * 1024 * 1024), null);
            time.Advance(System.TimeSpan.FromDays(2));
            var newerValid = reporter.RecordCrash("newer", "Ex", new string('x', 4 * 1024 * 1024), null);
            var corrupt = System.IO.Path.Combine(dir, "diagnostics", "crash-corrupt.json");
            System.IO.File.WriteAllBytes(corrupt, new byte[4 * 1024 * 1024]);
            System.IO.File.SetLastWriteTimeUtc(newerValid.FilePath, firstAt.AddDays(-10).UtcDateTime);
            System.IO.File.SetLastWriteTimeUtc(olderValid.FilePath, firstAt.AddDays(10).UtcDateTime);
            System.IO.File.SetLastWriteTimeUtc(corrupt, firstAt.AddDays(-1).UtcDateTime);

            reporter.Prune();

            Assert.True(System.IO.File.Exists(newerValid.FilePath));
            Assert.True(System.IO.File.Exists(olderValid.FilePath));
            Assert.False(System.IO.File.Exists(corrupt));
            var files = System.IO.Directory.EnumerateFiles(System.IO.Path.Combine(dir, "diagnostics"), "crash-*.json");
            Assert.True(files.Sum(file => new System.IO.FileInfo(file).Length) <= 10 * 1024 * 1024);
        }
        finally
        {
            System.IO.Directory.Delete(dir, recursive: true);
        }

        Assert.False(System.IO.Directory.Exists(dir));
    }

    [Fact]
    public void Prune_LengthMetadataFailureTreatsFileAsOversizedAndDeletesIt()
    {
        var now = new System.DateTimeOffset(2026, 1, 20, 12, 0, 0, System.TimeSpan.Zero);
        var dir = NewDir();
        try
        {
            var diagnostics = System.IO.Path.Combine(dir, "diagnostics");
            System.IO.Directory.CreateDirectory(diagnostics);
            var lengthFailure = System.IO.Path.Combine(diagnostics, "crash-length-failure.json");
            var record = new CrashDumpRecord("length-failure", "proc", "Ex", "msg", now.ToString("o"), "");
            var json = System.Text.Json.JsonSerializer.Serialize(record, CrashJsonContext.Default.CrashDumpRecord);
            System.IO.File.WriteAllText(lengthFailure, json);
            var metadataFailureObserved = false;
            var messages = new System.Collections.Generic.List<string>();
            var logger = new CallbackLogger(message =>
            {
                messages.Add(message);
                if (message.Contains("crash dump metadata read failed", System.StringComparison.Ordinal) &&
                    message.Contains(lengthFailure, System.StringComparison.Ordinal))
                    metadataFailureObserved = true;
            });
            var reporter = new CrashReporter(
                dir,
                logger,
                new ManualTimeProvider(now),
                file => file.FullName == lengthFailure
                    ? throw new System.IO.IOException("injected length failure")
                    : file.Length);

            reporter.Prune();

            Assert.True(metadataFailureObserved, string.Join(System.Environment.NewLine, messages));
            Assert.False(System.IO.File.Exists(lengthFailure));
        }
        finally
        {
            System.IO.Directory.Delete(dir, recursive: true);
        }

        Assert.False(System.IO.Directory.Exists(dir));
    }

    [Fact]
    public void RecordCrash_RedactsUserFilePathsFromMessage()
    {
        var dir = NewDir();
        var reporter = new CrashReporter(dir, NullLogger.Instance);
        var dump = reporter.RecordCrash("proc", "IOException", "Could not find file '/Users/alice/secret.txt'", null);

        var content = System.IO.File.ReadAllText(dump.FilePath);
        Assert.DoesNotContain("/Users/alice/secret.txt", content);
        Assert.DoesNotContain("alice", content);
    }

    [Fact]
    public void RecordCrash_RedactsUserFilePathsFromStackTrace()
    {
        var dir = NewDir();
        var reporter = new CrashReporter(dir, NullLogger.Instance);
        var dump = reporter.RecordCrash("proc", "Exception", "msg", "at Foo() in /home/bob/projects/secret.cs:line 42");

        var content = System.IO.File.ReadAllText(dump.FilePath);
        Assert.DoesNotContain("/home/bob", content);
        Assert.DoesNotContain("bob", content);
    }

    [Fact]
    public void RecordCrash_PreservesExceptionType()
    {
        var dir = NewDir();
        var reporter = new CrashReporter(dir, NullLogger.Instance);
        var dump = reporter.RecordCrash("proc", "NullReferenceException", "msg", null);

        var content = System.IO.File.ReadAllText(dump.FilePath);
        Assert.Contains("NullReferenceException", content);
    }

    [Fact]
    public void RecordCrash_WithNullStackTrace_Persists()
    {
        var dir = NewDir();
        var reporter = new CrashReporter(dir, NullLogger.Instance);
        var dump = reporter.RecordCrash("proc", "Ex", "msg", null);

        Assert.True(System.IO.File.Exists(dump.FilePath));
        var content = System.IO.File.ReadAllText(dump.FilePath);
        Assert.Contains("\"stackTrace\": \"\"", content);
    }

    [Fact]
    public void Register_WiresAppDomainUnhandledExceptionHandler()
    {
        var dir = NewDir();
        var reporter = new CrashReporter(dir, NullLogger.Instance);
        reporter.Register();

        Assert.True(CrashReporter.Registered);
    }

    [Fact]
    public void RecordCrash_SurvivesControlCharsInMessage()
    {
        var dir = NewDir();
        var reporter = new CrashReporter(dir, NullLogger.Instance);
        var msg = "bad value: \x00\x01\x02\x1f end";
        var dump = reporter.RecordCrash("proc", "Exception", msg, null);

        Assert.True(System.IO.File.Exists(dump.FilePath));
        var crashes = reporter.ListCrashes();
        Assert.Single(crashes);
        Assert.Contains("bad value", crashes[0].Message);
    }

    [Fact]
    public void HandleUnhandledException_WritesCrashDump()
    {
        var dir = NewDir();
        var reporter = new CrashReporter(dir, NullLogger.Instance);
        var ex = new System.InvalidOperationException("test crash at /Users/charlie/data.txt");

        reporter.HandleUnhandledException(ex, "cove-daemon");

        var crashes = reporter.ListCrashes();
        Assert.Single(crashes);
        Assert.Equal("InvalidOperationException", crashes[0].ExceptionType);
        var content = System.IO.File.ReadAllText(crashes[0].FilePath);
        Assert.DoesNotContain("/Users/charlie", content);
        Assert.DoesNotContain("charlie", content);
    }

    private sealed class CallbackLogger(System.Action<string> callback) : Microsoft.Extensions.Logging.ILogger
    {
        public System.IDisposable BeginScope<TState>(TState state) where TState : notnull => Scope.Instance;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            System.Exception? exception,
            System.Func<TState, System.Exception?, string> formatter) =>
            callback(formatter(state, exception));

        private sealed class Scope : System.IDisposable
        {
            public static readonly Scope Instance = new();

            public void Dispose()
            {
            }
        }
    }

}
