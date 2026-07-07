using System.Text.Json;
using Cove.Engine.Protocol;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class PaneScopeCommandTests
{
    [Fact]
    public async Task PaneScopeGet_NoStore_ReturnsNotReady()
    {
        var request = new ControlRequest("1", "cove://commands/pane.scope.get");
        var response = await EngineCommandRouter.RouteAsync(request);
        Assert.NotNull(response);
        Assert.False(response!.Ok);
        Assert.Equal("not_ready", response.Error!.Code);
    }

    [Fact]
    public async Task PaneScopeGet_WithStore_ReturnsDefaultScope()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cove-scope-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new PaneScopeStore(dir);
            var prm = JsonSerializer.SerializeToElement(new PaneScopeGetParams("pane-1"), CoveJsonContext.Default.PaneScopeGetParams);
            var request = new ControlRequest("1", "cove://commands/pane.scope.get", prm);
            var response = await EngineCommandRouter.RouteAsync(request, paneScopes: store);

            Assert.NotNull(response);
            Assert.True(response!.Ok);
            Assert.Equal("same-workspace", response.Data!.Value.GetProperty("scope").GetString());
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task PaneScopeSet_AllScope_RoundTrips()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cove-scope-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new PaneScopeStore(dir);
            var setPrm = JsonSerializer.SerializeToElement(new PaneScopeSetParams("pane-1", "all"), CoveJsonContext.Default.PaneScopeSetParams);
            var setReq = new ControlRequest("1", "cove://commands/pane.scope.set", setPrm);
            var setResp = await EngineCommandRouter.RouteAsync(setReq, paneScopes: store);
            Assert.True(setResp!.Ok);

            var getPrm = JsonSerializer.SerializeToElement(new PaneScopeGetParams("pane-1"), CoveJsonContext.Default.PaneScopeGetParams);
            var getReq = new ControlRequest("2", "cove://commands/pane.scope.get", getPrm);
            var getResp = await EngineCommandRouter.RouteAsync(getReq, paneScopes: store);
            Assert.Equal("all", getResp!.Data!.Value.GetProperty("scope").GetString());
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task PaneScopeSet_InvalidScope_ReturnsInvalidParams()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cove-scope-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new PaneScopeStore(dir);
            var prm = JsonSerializer.SerializeToElement(new PaneScopeSetParams("pane-1", "bogus"), CoveJsonContext.Default.PaneScopeSetParams);
            var request = new ControlRequest("1", "cove://commands/pane.scope.set", prm);
            var response = await EngineCommandRouter.RouteAsync(request, paneScopes: store);

            Assert.False(response!.Ok);
            Assert.Equal("invalid_params", response.Error!.Code);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
