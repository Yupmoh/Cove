using System.Text.Json;
using Cove.Protocol;

namespace Cove.Engine.Browser;

public static class BrowserAutomationCommands
{
    [CoveCommand("cove://commands/browser.snapshot")]
    public static Task<ControlResponse> Snapshot(EngineDispatchContext ctx)
    {
        if (ctx.BrowserAutomation is not { } bridge)
            return Task.FromResult(ctx.Fail("not_ready", "browser automation bridge not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BrowserAutomationSnapshotParams) is not { } p || string.IsNullOrEmpty(p.PaneId))
            return Task.FromResult(ctx.Fail("invalid_params", "snapshot requires paneId"));
        return RunAsync(ctx, bridge, p.PaneId, "snapshot", null, null, null);
    }

    [CoveCommand("cove://commands/browser.click")]
    public static Task<ControlResponse> Click(EngineDispatchContext ctx)
    {
        if (ctx.BrowserAutomation is not { } bridge)
            return Task.FromResult(ctx.Fail("not_ready", "browser automation bridge not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BrowserAutomationClickParams) is not { } p || string.IsNullOrEmpty(p.PaneId) || string.IsNullOrEmpty(p.Ref))
            return Task.FromResult(ctx.Fail("invalid_params", "click requires paneId and ref"));
        return RunAsync(ctx, bridge, p.PaneId, "click", p.Ref, null, null);
    }

    [CoveCommand("cove://commands/browser.fill")]
    public static Task<ControlResponse> Fill(EngineDispatchContext ctx)
    {
        if (ctx.BrowserAutomation is not { } bridge)
            return Task.FromResult(ctx.Fail("not_ready", "browser automation bridge not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BrowserAutomationFillParams) is not { } p || string.IsNullOrEmpty(p.PaneId) || string.IsNullOrEmpty(p.Ref) || p.Value is null)
            return Task.FromResult(ctx.Fail("invalid_params", "fill requires paneId, ref, and value"));
        return RunAsync(ctx, bridge, p.PaneId, "fill", p.Ref, p.Value, null);
    }

    [CoveCommand("cove://commands/browser.eval")]
    public static Task<ControlResponse> Eval(EngineDispatchContext ctx)
    {
        if (ctx.BrowserAutomation is not { } bridge)
            return Task.FromResult(ctx.Fail("not_ready", "browser automation bridge not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BrowserAutomationEvalParams) is not { } p || string.IsNullOrEmpty(p.PaneId) || string.IsNullOrEmpty(p.Js))
            return Task.FromResult(ctx.Fail("invalid_params", "eval requires paneId and js"));
        return RunAsync(ctx, bridge, p.PaneId, "eval", null, null, p.Js);
    }

    [CoveCommand("cove://commands/browser.screenshot")]
    public static Task<ControlResponse> Screenshot(EngineDispatchContext ctx)
    {
        if (ctx.BrowserAutomation is not { } bridge)
            return Task.FromResult(ctx.Fail("not_ready", "browser automation bridge not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BrowserScreenshotParams) is not { } p || string.IsNullOrEmpty(p.PaneId))
            return Task.FromResult(ctx.Fail("invalid_params", "screenshot requires paneId"));
        return RunAsync(ctx, bridge, p.PaneId, "screenshot", null, null, null);
    }

    [CoveCommand("cove://commands/browser.setUserAgent")]
    public static Task<ControlResponse> SetUserAgent(EngineDispatchContext ctx)
    {
        if (ctx.BrowserAutomation is not { } bridge)
            return Task.FromResult(ctx.Fail("not_ready", "browser automation bridge not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BrowserSetUserAgentParams) is not { } p || string.IsNullOrEmpty(p.PaneId) || string.IsNullOrEmpty(p.UserAgent))
            return Task.FromResult(ctx.Fail("invalid_params", "setUserAgent requires paneId and userAgent"));
        return RunAsync(ctx, bridge, p.PaneId, "setUserAgent", null, p.UserAgent, null);
    }

    [CoveCommand("cove://commands/browser.clear", Description = "clear a ref input/textarea/contenteditable and dispatch input+change")]
    public static Task<ControlResponse> Clear(EngineDispatchContext ctx)
    {
        if (ctx.BrowserAutomation is not { } bridge)
            return Task.FromResult(ctx.Fail("not_ready", "browser automation bridge not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BrowserAutomationClearParams) is not { } p || string.IsNullOrEmpty(p.PaneId) || string.IsNullOrEmpty(p.Ref))
            return Task.FromResult(ctx.Fail("invalid_params", "clear requires paneId and ref"));
        return RunAsync(ctx, bridge, p.PaneId, "clear", p.Ref, null, null);
    }

    [CoveCommand("cove://commands/browser.type", Description = "append text char-by-char into a ref with synthetic input events")]
    public static Task<ControlResponse> Type(EngineDispatchContext ctx)
    {
        if (ctx.BrowserAutomation is not { } bridge)
            return Task.FromResult(ctx.Fail("not_ready", "browser automation bridge not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BrowserAutomationTypeParams) is not { } p || string.IsNullOrEmpty(p.PaneId) || string.IsNullOrEmpty(p.Ref) || string.IsNullOrEmpty(p.Text))
            return Task.FromResult(ctx.Fail("invalid_params", "type requires paneId, ref, and text"));
        return RunAsync(ctx, bridge, p.PaneId, "type", p.Ref, p.Text, null);
    }

    [CoveCommand("cove://commands/browser.press", Description = "dispatch synthetic keydown/keypress/keyup for a named key (isTrusted=false, cannot fire browser default actions)")]
    public static Task<ControlResponse> Press(EngineDispatchContext ctx)
    {
        if (ctx.BrowserAutomation is not { } bridge)
            return Task.FromResult(ctx.Fail("not_ready", "browser automation bridge not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BrowserAutomationPressParams) is not { } p || string.IsNullOrEmpty(p.PaneId) || string.IsNullOrEmpty(p.Ref) || string.IsNullOrEmpty(p.Key))
            return Task.FromResult(ctx.Fail("invalid_params", "press requires paneId, ref, and key"));
        return RunAsync(ctx, bridge, p.PaneId, "press", p.Ref, p.Key, null);
    }

    [CoveCommand("cove://commands/browser.select", Description = "select an option in a <select> ref by its value")]
    public static Task<ControlResponse> Select(EngineDispatchContext ctx)
    {
        if (ctx.BrowserAutomation is not { } bridge)
            return Task.FromResult(ctx.Fail("not_ready", "browser automation bridge not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BrowserAutomationSelectParams) is not { } p || string.IsNullOrEmpty(p.PaneId) || string.IsNullOrEmpty(p.Ref) || string.IsNullOrEmpty(p.Value))
            return Task.FromResult(ctx.Fail("invalid_params", "select requires paneId, ref, and value"));
        return RunAsync(ctx, bridge, p.PaneId, "select", p.Ref, p.Value, null);
    }

    [CoveCommand("cove://commands/browser.scroll", Description = "scroll a ref element (or the window when ref is omitted) to absolute coordinates x,y — a missing axis defaults to 0")]
    public static Task<ControlResponse> Scroll(EngineDispatchContext ctx)
    {
        if (ctx.BrowserAutomation is not { } bridge)
            return Task.FromResult(ctx.Fail("not_ready", "browser automation bridge not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BrowserAutomationScrollParams) is not { } p || string.IsNullOrEmpty(p.PaneId))
            return Task.FromResult(ctx.Fail("invalid_params", "scroll requires paneId"));
        var value = JsonSerializer.Serialize(new BrowserScrollValue(p.X, p.Y), CoveJsonContext.Default.BrowserScrollValue);
        return RunAsync(ctx, bridge, p.PaneId, "scroll", p.Ref, value, null);
    }

    [CoveCommand("cove://commands/browser.wait", Description = "poll the page until a ref exists or text becomes visible, up to timeoutMs (default 2000, capped 8000ms under the bridge timeout)")]
    public static Task<ControlResponse> Wait(EngineDispatchContext ctx)
    {
        if (ctx.BrowserAutomation is not { } bridge)
            return Task.FromResult(ctx.Fail("not_ready", "browser automation bridge not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BrowserAutomationWaitParams) is not { } p || string.IsNullOrEmpty(p.PaneId))
            return Task.FromResult(ctx.Fail("invalid_params", "wait requires paneId"));
        if (string.IsNullOrEmpty(p.Ref) && string.IsNullOrEmpty(p.Text))
            return Task.FromResult(ctx.Fail("invalid_params", "wait requires ref or text"));
        var value = JsonSerializer.Serialize(new BrowserWaitValue(p.Text, p.TimeoutMs), CoveJsonContext.Default.BrowserWaitValue);
        return RunAsync(ctx, bridge, p.PaneId, "wait", p.Ref, value, null);
    }

    [CoveCommand("cove://commands/browser.get", Description = "read a whitelisted property (text,value,href,title,checked,disabled,visible) from a ref")]
    public static Task<ControlResponse> Get(EngineDispatchContext ctx)
    {
        if (ctx.BrowserAutomation is not { } bridge)
            return Task.FromResult(ctx.Fail("not_ready", "browser automation bridge not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BrowserAutomationGetParams) is not { } p || string.IsNullOrEmpty(p.PaneId) || string.IsNullOrEmpty(p.Ref) || string.IsNullOrEmpty(p.Prop))
            return Task.FromResult(ctx.Fail("invalid_params", "get requires paneId, ref, and prop"));
        return RunAsync(ctx, bridge, p.PaneId, "get", p.Ref, p.Prop, null);
    }

    [CoveCommand("cove://commands/browser.is", Description = "test a whitelisted state (visible,enabled,checked,editable) of a ref")]
    public static Task<ControlResponse> Is(EngineDispatchContext ctx)
    {
        if (ctx.BrowserAutomation is not { } bridge)
            return Task.FromResult(ctx.Fail("not_ready", "browser automation bridge not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BrowserAutomationIsParams) is not { } p || string.IsNullOrEmpty(p.PaneId) || string.IsNullOrEmpty(p.Ref) || string.IsNullOrEmpty(p.State))
            return Task.FromResult(ctx.Fail("invalid_params", "is requires paneId, ref, and state"));
        return RunAsync(ctx, bridge, p.PaneId, "is", p.Ref, p.State, null);
    }

    [CoveCommand("cove://commands/browser.automation.result")]
    public static Task<ControlResponse> Result(EngineDispatchContext ctx)
    {
        if (ctx.BrowserAutomation is not { } bridge)
            return Task.FromResult(ctx.Fail("not_ready", "browser automation bridge not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BrowserAutomationResultParams) is not { } p || string.IsNullOrEmpty(p.RequestId) || p.ResultJson is null)
            return Task.FromResult(ctx.Fail("invalid_params", "result requires requestId and resultJson"));
        var accepted = bridge.Complete(p.RequestId, p.ResultJson);
        return Task.FromResult(accepted ? ctx.Ok() : ctx.Fail("not_found", "request expired or unknown"));
    }

    private static async Task<ControlResponse> RunAsync(EngineDispatchContext ctx, BrowserAutomationBridge bridge, string paneId, string kind, string? refId, string? value, string? js)
    {
        var outcome = await bridge.ExecuteAsync(paneId, kind, refId, value, js, default).ConfigureAwait(false);
        if (!outcome.Ok)
            return ctx.Fail(outcome.ErrorCode ?? "error", outcome.ErrorMessage ?? "automation failed");
        return ctx.OkJson(outcome.ResultJson ?? "{}");
    }
}
