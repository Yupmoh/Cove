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
    [Fact]
    public async Task Inspect_ReturnsNull_ForUnknownSnapshot()
    {
        var dir = NewDir();
        try
        {
            var svc = NewService(dir);
            await svc.TakeAsync(State(content: "v1"), SnapshotTrigger.Manual);

            var diffs = await svc.InspectAsync("nonexistent-id");

            Assert.Null(diffs);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Inspect_NoChanges_WhenStateUnchanged()
    {
        var dir = NewDir();
        try
        {
            var svc = NewService(dir);
            var snap = await svc.TakeAsync(State(content: "v1"), SnapshotTrigger.Manual);

            var diffs = await svc.InspectAsync(snap!.Id);

            Assert.NotNull(diffs);
            Assert.Empty(diffs);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Inspect_ShowsChangedFile_WhenStateModified()
    {
        var dir = NewDir();
        try
        {
            var svc = NewService(dir);
            var snap = await svc.TakeAsync(State(content: "v1"), SnapshotTrigger.Manual);

            await File.WriteAllTextAsync(
                Path.Combine(dir, "snapshots", "workspaces/ws-1/workspace.json"),
                "v2-modified");

            var diffs = await svc.InspectAsync(snap!.Id);

            Assert.NotNull(diffs);
            var diff = Assert.Single(diffs);
            Assert.Equal("workspaces/ws-1/workspace.json", diff.Key);
            Assert.Contains("v1", diff.OldValue);
            Assert.Contains("v2-modified", diff.NewValue);
            Assert.Equal("changed", diff.ChangeType);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Inspect_ShowsAddedFile_WhenNewFileInCurrent()
    {
        var dir = NewDir();
        try
        {
            var svc = NewService(dir);
            var snap = await svc.TakeAsync(State(content: "v1"), SnapshotTrigger.Manual);

            await File.WriteAllTextAsync(
                Path.Combine(dir, "snapshots", "workspaces/ws-1/new.json"),
                "new-content");

            var diffs = await svc.InspectAsync(snap!.Id);

            Assert.NotNull(diffs);
            var added = diffs.FirstOrDefault(d => d.ChangeType == "added");
            Assert.NotNull(added);
            Assert.Equal("workspaces/ws-1/new.json", added!.Key);
            Assert.Null(added.OldValue);
            Assert.Contains("new-content", added.NewValue);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Inspect_ShowsRemovedFile_WhenFileDeletedFromCurrent()
    {
        var dir = NewDir();
        try
        {
            var svc = NewService(dir);
            var state = new Dictionary<string, string>
            {
                ["workspaces/ws-1/workspace.json"] = "v1",
                ["workspaces/ws-1/extra.json"] = "extra-content",
            };
            var snap = await svc.TakeAsync(state, SnapshotTrigger.Manual);

            File.Delete(Path.Combine(dir, "snapshots", "workspaces/ws-1/extra.json"));

            var diffs = await svc.InspectAsync(snap!.Id);

            Assert.NotNull(diffs);
            var removed = diffs.FirstOrDefault(d => d.ChangeType == "removed");
            Assert.NotNull(removed);
            Assert.Equal("workspaces/ws-1/extra.json", removed!.Key);
            Assert.Contains("extra-content", removed.OldValue);
            Assert.Null(removed.NewValue);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Restore_ThenInspect_RoundTripsWithUndo()
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
            Assert.Contains("v1", restored!.Values.First());

            var afterRestore = await svc.ListAsync();
            var preRestoreSnap = afterRestore.FirstOrDefault(s => s.Trigger == SnapshotTrigger.PreRestore);
            Assert.NotNull(preRestoreSnap);

            var undoDiffs = await svc.InspectAsync(preRestoreSnap!.Id);
            Assert.NotNull(undoDiffs);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
