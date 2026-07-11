using System.Text.Json;
using Cove.Engine.Browser;
using Cove.Engine.Layout;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class BrowserCreateRouteTests
{
    private static EngineDispatchContext CtxWith(LayoutService layout, BrowserNookManager? browser, JsonElement? paramsEl = null)
    {
        var p = paramsEl ?? JsonSerializer.SerializeToElement(
            new BrowserCreateParams("https://example.com", null, null), CoveJsonContext.Default.BrowserCreateParams);
        var request = new ControlRequest("1", "cove://commands/browser.create", p);
        return new EngineDispatchContext(request, nooks: null, layout: layout, browser: browser);
    }

    [Fact]
    public async Task CreateBrowserNook_OpensInManager()
    {
        var layout = new LayoutService();
        var browser = new BrowserNookManager();
        var ctx = CtxWith(layout, browser);
        var resp = await BrowserCommands.CreateBrowserNook(ctx);
        Assert.True(resp.Ok);
        var dto = resp.Data!.Value.Deserialize(CoveJsonContext.Default.BrowserNookDto)!;
        Assert.Equal("https://example.com", dto.CurrentUrl);
    }

    [Fact]
    public async Task CreateBrowserNook_GeneratesNookId()
    {
        var layout = new LayoutService();
        var browser = new BrowserNookManager();
        var ctx = CtxWith(layout, browser);
        var resp = await BrowserCommands.CreateBrowserNook(ctx);
        Assert.True(resp.Ok);
        var dto = resp.Data!.Value.Deserialize(CoveJsonContext.Default.BrowserNookDto)!;
        Assert.False(string.IsNullOrEmpty(dto.NookId));
        Assert.StartsWith("nook-", dto.NookId);
    }

    [Fact]
    public async Task CreateBrowserNook_NoManager_Fails()
    {
        var layout = new LayoutService();
        var ctx = CtxWith(layout, browser: null);
        var resp = await BrowserCommands.CreateBrowserNook(ctx);
        Assert.False(resp.Ok);
        Assert.Equal("not_ready", resp.Error!.Code);
    }

    [Fact]
    public async Task CreateBrowserNook_InvalidParams_Fails()
    {
        var layout = new LayoutService();
        var browser = new BrowserNookManager();
        var request = new ControlRequest("1", "cove://commands/browser.create", null);
        var ctx = new EngineDispatchContext(request, nooks: null, layout: layout, browser: browser);
        var resp = await BrowserCommands.CreateBrowserNook(ctx);
        Assert.False(resp.Ok);
        Assert.Equal("invalid_params", resp.Error!.Code);
    }
}
