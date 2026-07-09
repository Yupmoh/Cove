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
