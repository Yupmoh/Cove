using System.IO;
using System.Text.Json;
using Cove.Engine.Config;
using Cove.Engine.Protocol;
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
        finally { try { Directory.Delete(dir, true); } catch { } }
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
    public async Task EmitEvent_ReturnsOk()
    {
        var prm = JsonDocument.Parse("{\"event\":\"test.event\"}").RootElement.Clone();
        var request = new Cove.Protocol.ControlRequest("1", "cove://commands/emit_event", prm);
        var response = await EngineCommandRouter.RouteAsync(request);
        Assert.NotNull(response);
        Assert.True(response!.Ok);
    }

    [Fact]
    public async Task GetIpcEvents_ReturnsEmptyArray()
    {
        var request = new Cove.Protocol.ControlRequest("1", "cove://commands/get_ipc_events");
        var response = await EngineCommandRouter.RouteAsync(request);
        Assert.NotNull(response);
        Assert.True(response!.Ok);
        Assert.Equal(0, response.Data!.Value.GetProperty("events").GetArrayLength());
    }
}
