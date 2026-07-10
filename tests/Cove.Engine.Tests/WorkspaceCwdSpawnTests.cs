using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Cove.Engine;
using Cove.Engine.Pty;
using Cove.Engine.Workspaces;
using Cove.Platform.Pty;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class WorkspaceCwdSpawnTests
{
    private static Task<ControlResponse?> Route(PaneRegistry panes, WorkspaceManager ws, JsonElement prm) =>
        EngineCommandRouter.RouteAsync(new ControlRequest("1", "cove://commands/pane.spawn", prm), panes, null, ws);

    [Fact]
    public async Task Spawn_NoExplicitCwd_DefaultsToActiveWorkspaceDir()
    {
        if (OperatingSystem.IsWindows()) return;
        var dir = Path.Combine(Path.GetTempPath(), "cove-ws-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            using var reg = new PaneRegistry(PtyHostFactory.Create(NullLogger.Instance), NullLogger.Instance);
            await using var mgr = new WorkspaceManager();
            await mgr.CreateWorkspaceAsync("w", dir);
            var prm = JsonSerializer.SerializeToElement(
                new SpawnParams("/bin/sh", new[] { "-c", "sleep 5" }, null, null, 40, 10), CoveJsonContext.Default.SpawnParams);
            var resp = await Route(reg, mgr, prm);
            Assert.True(resp!.Ok);
            var paneId = resp.Data!.Value.GetProperty("paneId").GetString()!;
            var desc = reg.Descriptors().First(d => d.PaneId == paneId);
            Assert.Equal(dir, desc.Cwd);
        }
        finally { try { Directory.Delete(dir, true); } catch { /* best effort */ } }
    }

    [Fact]
    public async Task Spawn_ExplicitCwd_OverridesWorkspaceDir()
    {
        if (OperatingSystem.IsWindows()) return;
        var dir = Path.Combine(Path.GetTempPath(), "cove-ws-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            using var reg = new PaneRegistry(PtyHostFactory.Create(NullLogger.Instance), NullLogger.Instance);
            await using var mgr = new WorkspaceManager();
            await mgr.CreateWorkspaceAsync("w", dir);
            var prm = JsonSerializer.SerializeToElement(
                new SpawnParams("/bin/sh", new[] { "-c", "sleep 5" }, "/tmp", null, 40, 10), CoveJsonContext.Default.SpawnParams);
            var resp = await Route(reg, mgr, prm);
            Assert.True(resp!.Ok);
            var paneId = resp.Data!.Value.GetProperty("paneId").GetString()!;
            var desc = reg.Descriptors().First(d => d.PaneId == paneId);
            Assert.Equal("/tmp", desc.Cwd);
        }
        finally { try { Directory.Delete(dir, true); } catch { /* best effort */ } }
    }
}
