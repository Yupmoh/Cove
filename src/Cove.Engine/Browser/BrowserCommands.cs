using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Browser;

public static class BrowserCommands
{
    [CoveCommand("cove://commands/browser.open")]
    public static Task<ControlResponse> Open(EngineDispatchContext ctx)
    {
        if (ctx.Browser is not { } mgr)
            return Task.FromResult(ctx.Fail("not_ready", "browser manager not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BrowserOpenParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "browser open params required"));

        var nook = mgr.Open(p.NookId, p.Url);
        return Task.FromResult(ctx.Ok(ToDto(nook), CoveJsonContext.Default.BrowserNookDto));
    }

    [CoveCommand("cove://commands/browser.navigate")]
    public static Task<ControlResponse> Navigate(EngineDispatchContext ctx)
    {
        if (ctx.Browser is not { } mgr)
            return Task.FromResult(ctx.Fail("not_ready", "browser manager not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BrowserNavigateParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "browser navigate params required"));

        var nook = mgr.Navigate(p.NookId, p.Url);
        if (nook is null)
            return Task.FromResult(ctx.Fail("not_found", "browser nook not found"));
        return Task.FromResult(ctx.Ok(ToDto(nook), CoveJsonContext.Default.BrowserNookDto));
    }

    [CoveCommand("cove://commands/browser.back")]
    public static Task<ControlResponse> Back(EngineDispatchContext ctx)
    {
        if (ctx.Browser is not { } mgr)
            return Task.FromResult(ctx.Fail("not_ready", "browser manager not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BrowserNookRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "browser nook ref params required"));

        var nook = mgr.Back(p.NookId);
        if (nook is null)
            return Task.FromResult(ctx.Fail("not_found", "cannot go back"));
        return Task.FromResult(ctx.Ok(ToDto(nook), CoveJsonContext.Default.BrowserNookDto));
    }

    [CoveCommand("cove://commands/browser.forward")]
    public static Task<ControlResponse> Forward(EngineDispatchContext ctx)
    {
        if (ctx.Browser is not { } mgr)
            return Task.FromResult(ctx.Fail("not_ready", "browser manager not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BrowserNookRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "browser nook ref params required"));

        var nook = mgr.Forward(p.NookId);
        if (nook is null)
            return Task.FromResult(ctx.Fail("not_found", "cannot go forward"));
        return Task.FromResult(ctx.Ok(ToDto(nook), CoveJsonContext.Default.BrowserNookDto));
    }

    [CoveCommand("cove://commands/browser.reload")]
    public static Task<ControlResponse> Reload(EngineDispatchContext ctx)
    {
        if (ctx.Browser is not { } mgr)
            return Task.FromResult(ctx.Fail("not_ready", "browser manager not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BrowserNookRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "browser nook ref params required"));

        var url = mgr.Reload(p.NookId);
        if (url is null)
            return Task.FromResult(ctx.Fail("not_found", "browser nook not found"));
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/browser.close")]
    public static Task<ControlResponse> Close(EngineDispatchContext ctx)
    {
        if (ctx.Browser is not { } mgr)
            return Task.FromResult(ctx.Fail("not_ready", "browser manager not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BrowserNookRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "browser nook ref params required"));

        mgr.Close(p.NookId);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/browser.create")]
    public static Task<ControlResponse> CreateBrowserNook(EngineDispatchContext ctx)
    {
        if (ctx.Browser is not { } mgr)
            return Task.FromResult(ctx.Fail("not_ready", "browser manager not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BrowserCreateParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "browser create params required"));

        string nookId = "nook-" + System.Guid.NewGuid().ToString("N");
        var nook = mgr.Open(nookId, p.Url);
        return Task.FromResult(ctx.Ok(ToDto(nook), CoveJsonContext.Default.BrowserNookDto));
    }
    private static BrowserNookDto ToDto(BrowserNook nook) =>
        new(nook.NookId, nook.CurrentUrl, nook.History.ToList(), nook.HistoryIndex, nook.CanGoBack, nook.CanGoForward);
}
