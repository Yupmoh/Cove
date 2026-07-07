using Cove.Engine.Snapshots;
using Cove.Engine.Workspaces;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class SnapshotTests
{
    private sealed class NoOpLogger : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoOpDisposable.Instance;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
        private sealed class NoOpDisposable : IDisposable { public static readonly NoOpDisposable Instance = new(); public void Dispose() { } }
    }

    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-snap-" + Guid.NewGuid().ToString("N"));

    private static SnapshotService NewService(string dir)
    {
        Directory.CreateDirectory(dir);
        var snapshotsDir = Path.Combine(dir, "snapshots");
        return new SnapshotService(dir, snapshotsDir, new ProcessGitRunner(), new NoOpLogger());
    }

    private static IReadOnlyDictionary<string, string> State(string workspaceId = "ws-1", string content = "hello") =>
        new Dictionary<string, string> { [$"workspaces/{workspaceId}/workspace.json"] = content };

    [Fact]
    public async Task Take_CreatesSnapshot_WithContentHash()
    {
        var dir = NewDir();
        try
        {
            var svc = NewService(dir);
            var snap = await svc.TakeAsync(State(), SnapshotTrigger.Manual);

            Assert.NotNull(snap);
            Assert.False(string.IsNullOrEmpty(snap!.Id));
            Assert.False(string.IsNullOrEmpty(snap.Hash));
            Assert.Equal(SnapshotTrigger.Manual, snap.Trigger);
            Assert.Single(await svc.ListAsync());
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Take_Dedup_SkipsWhenHashMatches()
    {
        var dir = NewDir();
        try
        {
            var svc = NewService(dir);
            var state = State();

            var s1 = await svc.TakeAsync(state, SnapshotTrigger.Interval);
            var s2 = await svc.TakeAsync(state, SnapshotTrigger.Interval);

            Assert.Single(await svc.ListAsync());
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Take_Dedup_DoesNotSkipManualTrigger()
    {
        var dir = NewDir();
        try
        {
            var svc = NewService(dir);
            var state = State();

            var s1 = await svc.TakeAsync(state, SnapshotTrigger.Manual);
            var s2 = await svc.TakeAsync(state, SnapshotTrigger.Manual);

            Assert.Equal(2, (await svc.ListAsync()).Count);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task List_ReturnsMostRecentFirst()
    {
        var dir = NewDir();
        try
        {
            var svc = NewService(dir);
            await svc.TakeAsync(State(content: "v1"), SnapshotTrigger.Manual);
            await Task.Delay(1100);
            await svc.TakeAsync(State(content: "v2"), SnapshotTrigger.Manual);

            var list = await svc.ListAsync();
            Assert.Equal(2, list.Count);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Restore_ReturnsSnapshotContent()
    {
        var dir = NewDir();
        try
        {
            var svc = NewService(dir);
            var state = State(content: "restored-content");
            var snap = await svc.TakeAsync(state, SnapshotTrigger.Manual);

            var restored = await svc.RestoreAsync(snap!.Id);

            Assert.NotNull(restored);
            Assert.Contains(restored!, kv => kv.Value.Contains("restored-content"));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Prune_RunsGitGc_WithoutError()
    {
        var dir = NewDir();
        try
        {
            var svc = NewService(dir);
            await svc.TakeAsync(State(content: "v1"), SnapshotTrigger.Manual);

            await svc.PruneAsync();

            var list = await svc.ListAsync();
            Assert.Single(list);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Restore_FirstOfTwoSnapshots_ReturnsFirstContent()
    {
        var dir = NewDir();
        try
        {
            var svc = NewService(dir);
            await svc.TakeAsync(State(content: "FIRST"), SnapshotTrigger.Manual);
            await Task.Delay(1100);
            await svc.TakeAsync(State(content: "SECOND"), SnapshotTrigger.Manual);

            var list = await svc.ListAsync();
            var first = list.Last();

            var restored = await svc.RestoreAsync(first.Id);

            Assert.NotNull(restored);
            Assert.Contains("FIRST", restored!.Values.First());
            Assert.DoesNotContain("SECOND", restored.Values.First());
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Restore_FiresPreRestoreSafetyCommit_IsReversible()
    {
        var dir = NewDir();
        try
        {
            var svc = NewService(dir);
            await svc.TakeAsync(State(content: "v1"), SnapshotTrigger.Manual);
            await Task.Delay(1100);
            await svc.TakeAsync(State(content: "v2"), SnapshotTrigger.Manual);

            var list = await svc.ListAsync();
            var first = list.Last();

            var restored = await svc.RestoreAsync(first.Id);

            Assert.NotNull(restored);
            Assert.Contains("v1", restored!.Values.First());
            Assert.DoesNotContain("v2", restored.Values.First());

            var afterRestore = await svc.ListAsync();
            Assert.True(afterRestore.Count >= 3);
            var preRestoreCommit = afterRestore.FirstOrDefault(s => s.Trigger == SnapshotTrigger.PreRestore);
            Assert.NotNull(preRestoreCommit);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Take_FiltersSecrets_FromCommittedContent()
    {
        var dir = NewDir();
        try
        {
            var svc = NewService(dir);
            var state = new Dictionary<string, string>
            {
                ["workspaces/ws-1/workspace.json"] = "safe",
                ["config/.env"] = "SECRET=abc123",
                ["secrets/tokens.json"] = "{\"token\":\"xyz\"}",
                ["library/cookies"] = "cookie-data",
            };

            var snap = await svc.TakeAsync(state, SnapshotTrigger.Manual);
            Assert.NotNull(snap);

            var restored = await svc.RestoreAsync(snap!.Id);
            Assert.NotNull(restored);
            Assert.Contains("safe", restored!["workspaces/ws-1/workspace.json"]);
            Assert.DoesNotContain(restored, kv => kv.Key.Contains(".env"));
            Assert.DoesNotContain(restored, kv => kv.Key.Contains("secret"));
            Assert.DoesNotContain(restored, kv => kv.Key.Contains("cookies"));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
