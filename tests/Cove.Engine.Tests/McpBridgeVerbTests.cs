using System.IO;
using System.Text.Json;
using Cove.Engine.Config;
using Cove.Engine.Daemon;
using Cove.Engine.Protocol;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class McpBridgeVerbTests
{
    [Fact]
    public async Task GetBackendState_ReturnsVersionAndHeadless()
    {
        var request = new Cove.Protocol.ControlRequest("1", "cove://commands/get_backend_state");
        var response = await EngineCommandRouter.RouteAsync(request);
        Assert.NotNull(response);
        Assert.True(response!.Ok);
        Assert.Equal("headless", response.Data!.Value.GetProperty("mode").GetString());
        Assert.True(response.Data!.Value.GetProperty("headless").GetBoolean());
    }

    [Fact]
    public async Task ExecuteCommand_RedrivesToServiceBackedVerb()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cove-mcp-" + Guid.NewGuid().ToString("N"));
        try
        {
            var config = new ConfigService(dir, NullLogger.Instance);
            config.SetTheme("dracula");
            var subParams = JsonDocument.Parse("{\"command\":\"cove://commands/config.get\",\"params\":{\"key\":\"theme\"}}").RootElement.Clone();
            var request = new Cove.Protocol.ControlRequest("1", "cove://commands/execute_command", subParams);
            var response = await EngineCommandRouter.RouteAsync(request, config: config);
            Assert.NotNull(response);
            Assert.True(response!.Ok);
            Assert.Equal("dracula", response.Data!.Value.GetProperty("value").GetString());
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public async Task ExecuteCommand_RejectsSelfRedrive()
    {
        var subParams = JsonDocument.Parse("{\"command\":\"cove://commands/execute_command\"}").RootElement.Clone();
        var request = new Cove.Protocol.ControlRequest("1", "cove://commands/execute_command", subParams);
        var response = await EngineCommandRouter.RouteAsync(request);
        Assert.NotNull(response);
        Assert.False(response!.Ok);
        Assert.Equal("invalid_params", response.Error?.Code);
    }

    [Fact]
    public async Task CaptureNativeScreenshot_ReturnsNoRenderClient()
    {
        var request = new Cove.Protocol.ControlRequest("1", "cove://commands/capture_native_screenshot");
        var response = await EngineCommandRouter.RouteAsync(request);
        Assert.NotNull(response);
        Assert.False(response!.Ok);
        Assert.Equal("no_render_client", response.Error?.Code);
    }

    [Fact]
    public async Task ExecuteJs_ReturnsNoRenderClient()
    {
        var request = new Cove.Protocol.ControlRequest("1", "cove://commands/execute_js");
        var response = await EngineCommandRouter.RouteAsync(request);
        Assert.NotNull(response);
        Assert.False(response!.Ok);
        Assert.Equal("no_render_client", response.Error?.Code);
    }

    [Fact]
    public async Task EmitEvent_BroadcastsRealDaemonEvent()
    {
        var prm = JsonDocument.Parse("{\"event\":\"test.event\"}").RootElement.Clone();
        var request = new Cove.Protocol.ControlRequest("1", "cove://commands/emit_event", prm);
        string? emitted = null;
        var response = await EngineCommandRouter.RouteAsync(
            request,
            emitIpcEvent: (channel, _) => emitted = channel);
        Assert.NotNull(response);
        Assert.True(response!.Ok);
        Assert.Equal("test.event", emitted);
    }

    [Fact]
    public async Task IpcMonitor_RecordsRealDaemonEventsUntilStopped()
    {
        var events = new EngineEventRouter(
            CancellationToken.None);
        var started = await RouteMonitor(
            "start_ipc_monitor",
            events);
        var duplicateStart = await RouteMonitor(
            "start_ipc_monitor",
            events);
        events.PublishMutation(
            "cove://commands/nook.rename");
        var current = await RouteMonitor(
            "get_ipc_events",
            events);
        var stopped = await RouteMonitor(
            "stop_ipc_monitor",
            events);
        var duplicateStop = await RouteMonitor(
            "stop_ipc_monitor",
            events);
        events.PublishMutation(
            "cove://commands/nook.kill");
        var afterStop = await RouteMonitor(
            "get_ipc_events",
            events);

        Assert.True(started!.Ok);
        Assert.True(stopped!.Ok);
        Assert.Equal(
            "already_monitoring",
            duplicateStart!.Error!.Code);
        Assert.Equal(
            "not_monitoring",
            duplicateStop!.Error!.Code);
        Assert.Equal(
            1,
            current!.Data!.Value
                .GetProperty("events")
                .GetArrayLength());
        Assert.Equal(
            current.Data.Value.GetRawText(),
            afterStop!.Data!.Value.GetRawText());
    }

    [Fact]
    public async Task RenderVerb_WithGui_ReturnsCapabilityResult()
    {
        var request = new Cove.Protocol.ControlRequest(
            "1",
            "cove://commands/execute_js");
        var response = await EngineCommandRouter.RouteAsync(
            request,
            hasRenderClient: () => true);

        Assert.NotNull(response);
        Assert.False(response!.Ok);
        Assert.Equal(
            "unsupported_capability",
            response.Error?.Code);
    }

    private static Task<ControlResponse?> RouteMonitor(
        string verb,
        EngineEventRouter events) =>
        EngineCommandRouter.RouteAsync(
            new ControlRequest(
                verb,
                $"cove://commands/{verb}"),
            emitIpcEvent: events.BroadcastCompatibilityEvent,
            getIpcEvents: events.GetIpcEvents,
            startIpcMonitor: events.StartIpcMonitor,
            stopIpcMonitor: events.StopIpcMonitor,
            hasRenderClient: () => events.HasGuiClients);
}
