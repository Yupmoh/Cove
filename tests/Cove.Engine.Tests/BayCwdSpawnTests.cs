using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Cove.Engine;
using Cove.Engine.Bays;
using Cove.Engine.Pty;
using Cove.Platform.Pty;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Cove.Testing;

namespace Cove.Engine.Tests;

public sealed class BayCwdSpawnTests
{
    private static Task<ControlResponse?> Route(NookRegistry nooks, BayManager ws, JsonElement prm) =>
        EngineCommandRouter.RouteAsync(new ControlRequest("1", "cove://commands/nook.spawn", prm), nooks, null, ws);

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task Spawn_NoExplicitCwd_DefaultsToActiveBayDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cove-ws-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            using var reg = new NookRegistry(PtyHostFactory.Create(NullLogger.Instance), NullLogger.Instance);
            await using var mgr = new BayManager();
            await mgr.CreateBayAsync("w", dir);
            var prm = JsonSerializer.SerializeToElement(
                new SpawnParams("/bin/sh", new[] { "-c", "sleep 5" }, null, null, 40, 10), CoveJsonContext.Default.SpawnParams);
            var resp = await Route(reg, mgr, prm);
            Assert.True(resp!.Ok);
            var nookId = resp.Data!.Value.GetProperty("nookId").GetString()!;
            var desc = reg.Descriptors().First(d => d.NookId == nookId);
            Assert.Equal(dir, desc.Cwd);
        }
        finally { TestDirectory.Delete(dir); }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task Spawn_ExplicitCwd_OverridesBayDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cove-ws-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            using var reg = new NookRegistry(PtyHostFactory.Create(NullLogger.Instance), NullLogger.Instance);
            await using var mgr = new BayManager();
            await mgr.CreateBayAsync("w", dir);
            var prm = JsonSerializer.SerializeToElement(
                new SpawnParams("/bin/sh", new[] { "-c", "sleep 5" }, "/tmp", null, 40, 10), CoveJsonContext.Default.SpawnParams);
            var resp = await Route(reg, mgr, prm);
            Assert.True(resp!.Ok);
            var nookId = resp.Data!.Value.GetProperty("nookId").GetString()!;
            var desc = reg.Descriptors().First(d => d.NookId == nookId);
            Assert.Equal("/tmp", desc.Cwd);
        }
        finally { TestDirectory.Delete(dir); }
    }

    [Fact]
    public void ResolveWorkingDirectory_InheritedWins()
    {
        Assert.Equal("/inherited", NookRegistry.ResolveWorkingDirectory("/inherited", "/explicit", "/bay"));
    }

    [Fact]
    public void ResolveWorkingDirectory_ExplicitOverBay()
    {
        Assert.Equal("/explicit", NookRegistry.ResolveWorkingDirectory(null, "/explicit", "/bay"));
    }

    [Fact]
    public void ResolveWorkingDirectory_BayWhenNoInheritOrExplicit()
    {
        Assert.Equal("/bay", NookRegistry.ResolveWorkingDirectory(null, null, "/bay"));
    }

    [Fact]
    public void ResolveWorkingDirectory_FallsBackToHomeWhenAllEmpty()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.Equal(home, NookRegistry.ResolveWorkingDirectory(null, null, null));
    }
}
