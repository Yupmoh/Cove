using System.Text.Json;
using Cove.Engine;
using Cove.Engine.Launch;
using Cove.Engine.Pty;
using Cove.Platform.Pty;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class NookSpawnYoloTests
{
    private static NookRegistry NewNooks() => new(PtyHostFactory.Create(NullLogger.Instance), NullLogger.Instance);

    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-spawnyolo-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task NookSpawn_WithYolo_PersistsYoloOverride()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var dir = NewDir();
        try
        {
            using var nooks = NewNooks();
            var orch = new LaunchOrchestrator(overrideStore: new LauncherOverrideStore(dir));
            var request = new ControlRequest("1", "cove://commands/nook.spawn", JsonDocument.Parse("""{"command":"/bin/sh","args":["-c","sleep 30"],"adapter":"claude-code","yolo":true}""").RootElement);

            var response = await EngineCommandRouter.RouteAsync(request, nooks: nooks, launcher: orch);

            Assert.True(response!.Ok);
            var nookId = response.Data!.Value.GetProperty("nookId").GetString()!;
            var overrides = orch.GetOverrides(nookId);
            Assert.NotNull(overrides);
            Assert.True(overrides!.Yolo);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task NookSpawn_WithoutYolo_PersistsYoloFalse()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var dir = NewDir();
        try
        {
            using var nooks = NewNooks();
            var orch = new LaunchOrchestrator(overrideStore: new LauncherOverrideStore(dir));
            var request = new ControlRequest("1", "cove://commands/nook.spawn", JsonDocument.Parse("""{"command":"/bin/sh","args":["-c","sleep 30"],"adapter":"claude-code"}""").RootElement);

            var response = await EngineCommandRouter.RouteAsync(request, nooks: nooks, launcher: orch);

            Assert.True(response!.Ok);
            var nookId = response.Data!.Value.GetProperty("nookId").GetString()!;
            Assert.False(orch.GetOverrides(nookId)?.Yolo ?? false);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task NookSpawn_WithoutAdapter_DoesNotPersistOverride()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var dir = NewDir();
        try
        {
            using var nooks = NewNooks();
            var orch = new LaunchOrchestrator(overrideStore: new LauncherOverrideStore(dir));
            var request = new ControlRequest("1", "cove://commands/nook.spawn", JsonDocument.Parse("""{"command":"/bin/sh","args":["-c","sleep 30"],"yolo":true}""").RootElement);

            var response = await EngineCommandRouter.RouteAsync(request, nooks: nooks, launcher: orch);

            Assert.True(response!.Ok);
            var nookId = response.Data!.Value.GetProperty("nookId").GetString()!;
            Assert.Null(orch.GetOverrides(nookId));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
