using System.Text.Json;
using System.Threading.Tasks;
using Cove.Protocol;

namespace Cove.Engine.Protocol;

internal static class McpBridgeCommands
{
    [CoveCommand("cove://commands/execute_command", Description = "mcp-bridge execute_command (PL-30, headless-safe)")]
    public static async Task<ControlResponse> ExecuteCommand(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.ExecuteCommandParams) is not { } p)
            return ctx.Fail("invalid_params", "execute_command params required");
        if (p.Command == "cove://commands/execute_command")
            return ctx.Fail("invalid_params", "execute_command cannot redrive itself");
        if (ctx.Redrive is null)
            return ctx.Fail("not_ready", "redrive unavailable");
        var subReq = new ControlRequest(
            ctx.Request.Id + "-sub",
            p.Command,
            p.Params,
            ctx.Request.Source,
            ctx.Request.CallerNookId);
        var subResp = await ctx.Redrive(subReq);
        return subResp ?? ctx.Fail("not_found", $"command '{p.Command}' not found");
    }

    [CoveCommand("cove://commands/get_backend_state", Description = "mcp-bridge get_backend_state (PL-30, headless-safe)")]
    public static Task<ControlResponse> GetBackendState(EngineDispatchContext ctx)
    {
        var state = new BackendState(Cove.Platform.CoveBuild.InformationalVersion, "headless", true);
        return Task.FromResult(ctx.Ok(state, CoveJsonContext.Default.BackendState));
    }

    [CoveCommand("cove://commands/emit_event", Description = "mcp-bridge emit_event (PL-30, headless-safe)")]
    public static Task<ControlResponse> EmitEvent(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.EmitEventParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "emit_event params required"));
        if (ctx.EmitIpcEvent is not { } emit)
            return Task.FromResult(ctx.Fail("unsupported_capability", "daemon event emission unavailable"));
        emit(p.Event, p.Payload);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/get_ipc_events", Description = "mcp-bridge get_ipc_events (PL-30, headless-safe)")]
    public static Task<ControlResponse> GetIpcEvents(EngineDispatchContext ctx)
    {
        if (ctx.GetIpcEvents is not { } getEvents)
            return Task.FromResult(ctx.Fail("unsupported_capability", "daemon event monitoring unavailable"));
        return Task.FromResult(ctx.Ok(getEvents(), CoveJsonContext.Default.IpcEventLog));
    }

    [CoveCommand("cove://commands/start_ipc_monitor", Description = "mcp-bridge start_ipc_monitor (PL-30, headless-safe)")]
    public static Task<ControlResponse> StartIpcMonitor(EngineDispatchContext ctx)
    {
        if (ctx.StartIpcMonitor is not { } start)
            return Task.FromResult(ctx.Fail("unsupported_capability", "daemon event monitoring unavailable"));
        return Task.FromResult(start()
            ? ctx.Ok()
            : ctx.Fail("already_monitoring", "daemon event monitor is already running"));
    }

    [CoveCommand("cove://commands/stop_ipc_monitor", Description = "mcp-bridge stop_ipc_monitor (PL-30, headless-safe)")]
    public static Task<ControlResponse> StopIpcMonitor(EngineDispatchContext ctx)
    {
        if (ctx.StopIpcMonitor is not { } stop)
            return Task.FromResult(ctx.Fail("unsupported_capability", "daemon event monitoring unavailable"));
        return Task.FromResult(stop()
            ? ctx.Ok()
            : ctx.Fail("not_monitoring", "daemon event monitor is not running"));
    }

    [CoveCommand("cove://commands/capture_native_screenshot", Description = "mcp-bridge capture_native_screenshot (PL-30, render-bound)")]
    public static Task<ControlResponse> CaptureNativeScreenshot(EngineDispatchContext ctx)
        => Task.FromResult(RenderCapability(ctx, "capture_native_screenshot"));

    [CoveCommand("cove://commands/execute_js", Description = "mcp-bridge execute_js (PL-30, render-bound)")]
    public static Task<ControlResponse> ExecuteJs(EngineDispatchContext ctx)
        => Task.FromResult(RenderCapability(ctx, "execute_js"));

    [CoveCommand("cove://commands/list_windows", Description = "mcp-bridge list_windows (PL-30, render-bound)")]
    public static Task<ControlResponse> ListWindows(EngineDispatchContext ctx)
        => Task.FromResult(RenderCapability(ctx, "list_windows"));

    [CoveCommand("cove://commands/get_window_info", Description = "mcp-bridge get_window_info (PL-30, render-bound)")]
    public static Task<ControlResponse> GetWindowInfo(EngineDispatchContext ctx)
        => Task.FromResult(RenderCapability(ctx, "get_window_info"));

    private static ControlResponse RenderCapability(
        EngineDispatchContext ctx,
        string capability)
    {
        if (ctx.HasRenderClient?.Invoke() != true)
            return ctx.Fail("no_render_client", "render-bound verb requires a GUI client");
        return ctx.Fail(
            "unsupported_capability",
            $"registered GUI client does not support {capability}");
    }
}
