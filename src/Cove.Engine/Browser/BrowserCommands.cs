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

        var pane = mgr.Open(p.PaneId, p.Url);
        return Task.FromResult(ctx.Ok(ToDto(pane), CoveJsonContext.Default.BrowserPaneDto));
    }

    [CoveCommand("cove://commands/browser.navigate")]
    public static Task<ControlResponse> Navigate(EngineDispatchContext ctx)
    {
        if (ctx.Browser is not { } mgr)
            return Task.FromResult(ctx.Fail("not_ready", "browser manager not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BrowserNavigateParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "browser navigate params required"));

        var pane = mgr.Navigate(p.PaneId, p.Url);
        if (pane is null)
            return Task.FromResult(ctx.Fail("not_found", "browser pane not found"));
        return Task.FromResult(ctx.Ok(ToDto(pane), CoveJsonContext.Default.BrowserPaneDto));
    }

    [CoveCommand("cove://commands/browser.back")]
    public static Task<ControlResponse> Back(EngineDispatchContext ctx)
    {
        if (ctx.Browser is not { } mgr)
            return Task.FromResult(ctx.Fail("not_ready", "browser manager not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BrowserPaneRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "browser pane ref params required"));

        var pane = mgr.Back(p.PaneId);
        if (pane is null)
            return Task.FromResult(ctx.Fail("not_found", "cannot go back"));
        return Task.FromResult(ctx.Ok(ToDto(pane), CoveJsonContext.Default.BrowserPaneDto));
    }

    [CoveCommand("cove://commands/browser.forward")]
    public static Task<ControlResponse> Forward(EngineDispatchContext ctx)
    {
        if (ctx.Browser is not { } mgr)
            return Task.FromResult(ctx.Fail("not_ready", "browser manager not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BrowserPaneRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "browser pane ref params required"));

        var pane = mgr.Forward(p.PaneId);
        if (pane is null)
            return Task.FromResult(ctx.Fail("not_found", "cannot go forward"));
        return Task.FromResult(ctx.Ok(ToDto(pane), CoveJsonContext.Default.BrowserPaneDto));
    }

    [CoveCommand("cove://commands/browser.reload")]
    public static Task<ControlResponse> Reload(EngineDispatchContext ctx)
    {
        if (ctx.Browser is not { } mgr)
            return Task.FromResult(ctx.Fail("not_ready", "browser manager not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BrowserPaneRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "browser pane ref params required"));

        var url = mgr.Reload(p.PaneId);
        if (url is null)
            return Task.FromResult(ctx.Fail("not_found", "browser pane not found"));
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/browser.close")]
    public static Task<ControlResponse> Close(EngineDispatchContext ctx)
    {
        if (ctx.Browser is not { } mgr)
            return Task.FromResult(ctx.Fail("not_ready", "browser manager not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BrowserPaneRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "browser pane ref params required"));

        mgr.Close(p.PaneId);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/browser.create")]
    public static Task<ControlResponse> CreateBrowserPane(EngineDispatchContext ctx)
    {
        if (ctx.Browser is not { } mgr)
            return Task.FromResult(ctx.Fail("not_ready", "browser manager not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BrowserCreateParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "browser create params required"));

        string paneId = "pane-" + System.Guid.NewGuid().ToString("N");
        var pane = mgr.Open(paneId, p.Url);
        return Task.FromResult(ctx.Ok(ToDto(pane), CoveJsonContext.Default.BrowserPaneDto));
    }
    private static BrowserPaneDto ToDto(BrowserPane pane) =>
        new(pane.PaneId, pane.CurrentUrl, pane.History.ToList(), pane.HistoryIndex, pane.CanGoBack, pane.CanGoForward);
}
