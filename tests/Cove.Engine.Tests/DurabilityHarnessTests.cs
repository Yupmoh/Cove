using System.Text.Json;
using Cove.Engine.Daemon;
using Cove.Engine.Restart;
using Cove.Engine.Snapshots;
using Cove.Engine.Workspaces;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class DurabilityHarnessTests
{
    private sealed class NoOpLogger : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoOpDisposable.Instance;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
        private sealed class NoOpDisposable : IDisposable { public static readonly NoOpDisposable Instance = new(); public void Dispose() { } }
    }

    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-dur-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CorruptWorkspaceJson_FallsBackToBakFile()
    {
        var dir = NewDir();
        try
        {
            var wsDir = Path.Combine(dir, "workspaces", "default");
            Directory.CreateDirectory(wsDir);
            var layout = new Cove.Persistence.WorkspaceSnapshot { Id = "default", Name = "default", ProjectDir = System.Environment.CurrentDirectory };
            var panes = new Cove.Persistence.PaneDescriptor[0];
            Cove.Engine.Layout.WorkspacePersistence.Save(layout, panes, wsDir);
            Cove.Engine.Layout.WorkspacePersistence.Save(layout, panes, wsDir);

            var wsPath = Path.Combine(wsDir, "workspace.json");
            var bakPath = wsPath + ".bak";
            Assert.True(File.Exists(bakPath));
            await File.WriteAllTextAsync(wsPath, "{ CORRUPT JSON {{{").ConfigureAwait(true);

            var (loaded, _) = Cove.Engine.Layout.WorkspacePersistence.Load(wsDir, new NoOpLogger());
            Assert.NotNull(loaded);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task OutOfBandGitWorktreeAdd_ReflectsInOrphanList()
    {
        var repo = Path.Combine(Path.GetTempPath(), "cove-dur-repo-" + Guid.NewGuid().ToString("N"));
        var wtPath = Path.Combine(Path.GetTempPath(), "cove-dur-wt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repo);
        var git = new ProcessGitRunner();
        try
        {
            await git.RunAsync(repo, ["init"]);
            await git.RunAsync(repo, ["config", "user.email", "t@example.com"]);
            await git.RunAsync(repo, ["config", "user.name", "t"]);
            File.WriteAllText(Path.Combine(repo, "README.md"), "hi");
            await git.RunAsync(repo, ["add", "."]);
            await git.RunAsync(repo, ["commit", "-m", "init"]);

            var svc = new WorktreeService(git);
            var orphansBefore = await svc.OrphansAsync(repo, []);
            Assert.Empty(orphansBefore);

            await git.RunAsync(repo, ["worktree", "add", wtPath, "-b", "feature"]);

            var orphansAfter = await svc.OrphansAsync(repo, []);
            Assert.Contains(orphansAfter, p => p == wtPath || p.Contains(Path.GetFileName(wtPath)));
        }
        finally
        {
            try { Directory.Delete(repo, true); } catch { }
            try { Directory.Delete(wtPath, true); } catch { }
        }
    }

    [Fact]
    public async Task ControlPlaneRoundTrip_CreateAndListWorkspace()
    {
        if (System.OperatingSystem.IsWindows())
            return;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;
        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var createReq = new ControlRequest("1", "cove://commands/workspace.create",
            System.Text.Json.JsonSerializer.SerializeToElement(
                new WorkspaceCreateParams("test-ws", "/tmp", null),
                Cove.Engine.Workspaces.WorkspacesJsonContext.Default.WorkspaceCreateParams));
        await ctl.WriteFrameAsync(FrameType.Request, 0, ControlCodec.Encode(createReq), ct);
        ControlResponse createResp = await ReadResponseAsync(ctl, "1", ct);
        Assert.True(createResp.Ok, createResp.Error?.Message);

        var listReq = new ControlRequest("2", "cove://commands/workspace.list", null);
        await ctl.WriteFrameAsync(FrameType.Request, 0, ControlCodec.Encode(listReq), ct);
        ControlResponse listResp = await ReadResponseAsync(ctl, "2", ct);
        Assert.True(listResp.Ok, listResp.Error?.Message);
        var list = listResp.Data!.Value.Deserialize(Cove.Engine.Workspaces.WorkspacesJsonContext.Default.WorkspaceListResult)!;
        Assert.Contains(list.Workspaces, w => w.Name == "test-ws");
    }

    [Fact]
    public async Task SnapshotTakeAndRestore_RoundTripsContent()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var dir = NewDir();
        try
        {
            var svc = new SnapshotService(dir, Path.Combine(dir, "snapshots"), new ProcessGitRunner(), new NoOpLogger());
            var content = new Dictionary<string, string> { ["workspaces/ws-1/workspace.json"] = "content-v1" };

            var snap = await svc.TakeAsync(content, SnapshotTrigger.Manual);
            Assert.NotNull(snap);

            var list = await svc.ListAsync();
            Assert.Single(list);

            var restored = await svc.RestoreAsync(snap!.Id);
            Assert.NotNull(restored);
            Assert.Contains("content-v1", restored!["workspaces/ws-1/workspace.json"]);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    private static async Task<ControlResponse> ReadResponseAsync(FrameConnection conn, string id, CancellationToken ct)
    {
        while (true)
        {
            Frame f = (await conn.ReadFrameAsync(ct))!.Value;
            if (f.Header.Type != FrameType.Response)
                continue;
            ControlResponse r = ControlCodec.DecodeResponse(f.Payload);
            if (r.Id == id)
                return r;
        }
    }
}
