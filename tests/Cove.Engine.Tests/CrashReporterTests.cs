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
        var reporter = new CrashReporter(dir, NullLogger.Instance);
        reporter.RecordCrash("first", "Ex1", "msg1", null);
        System.Threading.Thread.Sleep(20);
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
        var reporter = new CrashReporter(dir, NullLogger.Instance);
        var dump = reporter.RecordCrash("old", "Ex", "msg", null);

        var oldPath = dump.FilePath;
        var oldTime = System.DateTimeOffset.UtcNow.AddDays(-10);
        System.IO.File.SetCreationTimeUtc(oldPath, oldTime.UtcDateTime);

        reporter.Prune();
        Assert.False(System.IO.File.Exists(oldPath));
    }

    [Fact]
    public void Prune_EnforcesSizeCap()
    {
        var dir = NewDir();
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
}
