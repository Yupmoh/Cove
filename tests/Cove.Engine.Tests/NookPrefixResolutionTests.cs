using System.Text.Json;
using Cove.Engine;
using Cove.Engine.Pty;
using Cove.Platform.Pty;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class NookPrefixResolutionTests
{
    private static NookRegistry NewNooks()
        => new(PtyHostFactory.Create(NullLogger.Instance), NullLogger.Instance);

    private static string SpawnNook(NookRegistry nooks)
    {
        var req = new ControlRequest("1", "cove://commands/nook.spawn",
            JsonDocument.Parse("""{"command":"/bin/sh","args":["-c","sleep 30"]}""").RootElement.Clone());
        var resp = EngineCommandRouter.RouteAsync(req, nooks: nooks).GetAwaiter().GetResult();
        return resp!.Data!.Value.GetProperty("nookId").GetString()!;
    }

    [Fact]
    public async Task NookKill_UniquePrefix_Resolves()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var nooks = NewNooks();
        var nook = SpawnNook(nooks);
        var prefix = nook.Substring(0, 8);
        var prm = JsonDocument.Parse($"{{\"nookId\":\"{prefix}\"}}").RootElement.Clone();
        var request = new ControlRequest("1", "cove://commands/nook.kill", prm);
        var response = await EngineCommandRouter.RouteAsync(request, nooks: nooks);
        Assert.NotNull(response);
        Assert.True(response!.Ok);
        Assert.DoesNotContain(nooks.List(), p => p.NookId == nook);
    }

    [Fact]
    public async Task NookRename_UniquePrefix_Resolves()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var nooks = NewNooks();
        string target = "", other = "";
        try
        {
            target = SpawnNook(nooks);
            other = SpawnNook(nooks);
            var prefix = target.Substring(0, 13);
            var prm = JsonDocument.Parse($"{{\"nookId\":\"{prefix}\",\"title\":\"renamed\"}}").RootElement.Clone();
            var request = new ControlRequest("1", "cove://commands/nook.rename", prm);
            var response = await EngineCommandRouter.RouteAsync(request, nooks: nooks);
            Assert.NotNull(response);
            Assert.True(response!.Ok);
            var info = System.Linq.Enumerable.First(nooks.List(), p => p.NookId == target);
            Assert.Equal("renamed", info.Title);
        }
        finally
        {
            try { nooks.Kill(target); } catch { }
            try { nooks.Kill(other); } catch { }
        }
    }

    [Fact]
    public async Task NookWrite_UniquePrefix_Resolves()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var nooks = NewNooks();
        string nook = "";
        try
        {
            nook = SpawnNook(nooks);
            var prefix = nook.Substring(0, 8);
            var prm = JsonDocument.Parse($"{{\"nookId\":\"{prefix}\",\"dataBase64\":\"aGk=\"}}").RootElement.Clone();
            var request = new ControlRequest("1", "cove://commands/nook.write", prm);
            var response = await EngineCommandRouter.RouteAsync(request, nooks: nooks);
            Assert.NotNull(response);
            Assert.True(response!.Ok);
        }
        finally { try { nooks.Kill(nook); } catch { } }
    }

    [Fact]
    public async Task NookResize_UniquePrefix_Resolves()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var nooks = NewNooks();
        string nook = "";
        try
        {
            nook = SpawnNook(nooks);
            var prefix = nook.Substring(0, 8);
            var prm = JsonDocument.Parse($"{{\"nookId\":\"{prefix}\",\"cols\":100,\"rows\":40}}").RootElement.Clone();
            var request = new ControlRequest("1", "cove://commands/nook.resize", prm);
            var response = await EngineCommandRouter.RouteAsync(request, nooks: nooks);
            Assert.NotNull(response);
            Assert.True(response!.Ok);
        }
        finally { try { nooks.Kill(nook); } catch { } }
    }

    [Fact]
    public async Task NookWrite_AmbiguousPrefix_ReturnsAmbiguousId()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var nooks = NewNooks();
        string a = "", b = "";
        try
        {
            a = SpawnNook(nooks);
            b = SpawnNook(nooks);
            var prm = JsonDocument.Parse("{\"nookId\":\"nook-\",\"dataBase64\":\"aGk=\"}").RootElement.Clone();
            var request = new ControlRequest("1", "cove://commands/nook.write", prm);
            var response = await EngineCommandRouter.RouteAsync(request, nooks: nooks);
            Assert.NotNull(response);
            Assert.False(response!.Ok);
            Assert.Equal("ambiguous_id", response.Error?.Code);
        }
        finally
        {
            try { nooks.Kill(a); } catch { }
            try { nooks.Kill(b); } catch { }
        }
    }

    [Fact]
    public async Task NookKill_UnknownPrefix_ReturnsNotFound()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var nooks = NewNooks();
        string nook = "";
        try
        {
            nook = SpawnNook(nooks);
            var prm = JsonDocument.Parse("{\"nookId\":\"zzz-nope\"}").RootElement.Clone();
            var request = new ControlRequest("1", "cove://commands/nook.kill", prm);
            var response = await EngineCommandRouter.RouteAsync(request, nooks: nooks);
            Assert.NotNull(response);
            Assert.False(response!.Ok);
            Assert.Equal("not_found", response.Error?.Code);
        }
        finally { try { nooks.Kill(nook); } catch { } }
    }
}
