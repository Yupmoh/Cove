using System.Text.Json;
using Cove.Engine.Protocol;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class NookScopeCommandTests
{
    [Fact]
    public async Task NookScopeGet_NoStore_ReturnsNotReady()
    {
        var request = new ControlRequest("1", "cove://commands/nook.scope.get");
        var response = await EngineCommandRouter.RouteAsync(request);
        Assert.NotNull(response);
        Assert.False(response!.Ok);
        Assert.Equal("not_ready", response.Error!.Code);
    }

    [Fact]
    public async Task NookScopeGet_WithStore_ReturnsDefaultScope()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cove-scope-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new NookScopeStore(dir);
            var prm = JsonSerializer.SerializeToElement(new NookScopeGetParams("nook-1"), CoveJsonContext.Default.NookScopeGetParams);
            var request = new ControlRequest("1", "cove://commands/nook.scope.get", prm);
            var response = await EngineCommandRouter.RouteAsync(request, nookScopes: store);

            Assert.NotNull(response);
            Assert.True(response!.Ok);
            Assert.Equal("same-bay", response.Data!.Value.GetProperty("scope").GetString());
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task NookScopeSet_AllScope_RoundTrips()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cove-scope-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new NookScopeStore(dir);
            var setPrm = JsonSerializer.SerializeToElement(new NookScopeSetParams("nook-1", "all"), CoveJsonContext.Default.NookScopeSetParams);
            var setReq = new ControlRequest("1", "cove://commands/nook.scope.set", setPrm);
            var setResp = await EngineCommandRouter.RouteAsync(setReq, nookScopes: store);
            Assert.True(setResp!.Ok);

            var getPrm = JsonSerializer.SerializeToElement(new NookScopeGetParams("nook-1"), CoveJsonContext.Default.NookScopeGetParams);
            var getReq = new ControlRequest("2", "cove://commands/nook.scope.get", getPrm);
            var getResp = await EngineCommandRouter.RouteAsync(getReq, nookScopes: store);
            Assert.Equal("all", getResp!.Data!.Value.GetProperty("scope").GetString());
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task NookScopeSet_InvalidScope_ReturnsInvalidParams()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cove-scope-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new NookScopeStore(dir);
            var prm = JsonSerializer.SerializeToElement(new NookScopeSetParams("nook-1", "bogus"), CoveJsonContext.Default.NookScopeSetParams);
            var request = new ControlRequest("1", "cove://commands/nook.scope.set", prm);
            var response = await EngineCommandRouter.RouteAsync(request, nookScopes: store);

            Assert.False(response!.Ok);
            Assert.Equal("invalid_params", response.Error!.Code);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
