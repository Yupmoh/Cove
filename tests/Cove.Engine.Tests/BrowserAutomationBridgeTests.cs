using System.Text.Json;
using Cove.Engine.Browser;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class BrowserAutomationBridgeTests
{
    [Fact]
    public async Task ExecuteAsync_CompletedByResult_ReturnsResultJson()
    {
        BrowserAutomationExecEvent? emitted = null;
        BrowserAutomationBridge? bridge = null;
        bridge = new BrowserAutomationBridge(e =>
        {
            emitted = e;
            Task.Run(() => bridge!.Complete(e.RequestId, "{\"entries\":[]}"));
        }, NullLogger.Instance, TimeSpan.FromSeconds(5));

        var outcome = await bridge.ExecuteAsync("pane-1", "snapshot", null, null, null, default);

        Assert.True(outcome.Ok);
        Assert.Equal("{\"entries\":[]}", outcome.ResultJson);
        Assert.NotNull(emitted);
        Assert.Equal("pane-1", emitted!.PaneId);
        Assert.Equal("snapshot", emitted.Kind);
        Assert.Equal(0, bridge.PendingCount);
    }

    [Fact]
    public async Task ExecuteAsync_NoGuiResponds_ReturnsAdrift()
    {
        var bridge = new BrowserAutomationBridge(_ => { }, NullLogger.Instance, TimeSpan.FromMilliseconds(80));

        var outcome = await bridge.ExecuteAsync("pane-1", "click", "e1", null, null, default);

        Assert.False(outcome.Ok);
        Assert.Equal("adrift", outcome.ErrorCode);
        Assert.Equal(0, bridge.PendingCount);
    }

    [Fact]
    public void Complete_UnknownRequest_ReturnsFalse()
    {
        var bridge = new BrowserAutomationBridge(_ => { }, NullLogger.Instance);

        Assert.False(bridge.Complete("nope", "{}"));
    }

    [Fact]
    public async Task ExecuteAsync_CarriesVerbFields()
    {
        BrowserAutomationExecEvent? emitted = null;
        BrowserAutomationBridge? bridge = null;
        bridge = new BrowserAutomationBridge(e =>
        {
            emitted = e;
            Task.Run(() => bridge!.Complete(e.RequestId, "{\"ok\":true}"));
        }, NullLogger.Instance);

        await bridge.ExecuteAsync("pane-9", "fill", "e3", "hello", null, default);

        Assert.Equal("fill", emitted!.Kind);
        Assert.Equal("e3", emitted.Ref);
        Assert.Equal("hello", emitted.Value);
    }
}

public sealed class BrowserAutomationCommandsTests
{
    private static ControlRequest Req(string uri, string paramsJson)
    {
        using var doc = JsonDocument.Parse(paramsJson);
        return new ControlRequest("t1", uri, doc.RootElement.Clone(), "user:test");
    }

    private static BrowserAutomationBridge EchoBridge()
    {
        BrowserAutomationBridge? bridge = null;
        bridge = new BrowserAutomationBridge(e =>
        {
            Task.Run(() => bridge!.Complete(e.RequestId, "{\"kind\":\"" + e.Kind + "\"}"));
        }, NullLogger.Instance, TimeSpan.FromSeconds(5));
        return bridge;
    }

    private static (BrowserAutomationBridge Bridge, Func<BrowserAutomationExecEvent?> Emitted) CapturingBridge()
    {
        BrowserAutomationExecEvent? emitted = null;
        BrowserAutomationBridge? bridge = null;
        bridge = new BrowserAutomationBridge(e =>
        {
            emitted = e;
            Task.Run(() => bridge!.Complete(e.RequestId, "{\"kind\":\"" + e.Kind + "\"}"));
        }, NullLogger.Instance, TimeSpan.FromSeconds(5));
        return (bridge, () => emitted);
    }

    [Fact]
    public async Task Snapshot_NoBridge_FailsNotReady()
    {
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.snapshot", "{\"paneId\":\"p1\"}"));

        var resp = await BrowserAutomationCommands.Snapshot(ctx);

        Assert.False(resp.Ok);
        Assert.Equal("not_ready", resp.Error!.Code);
    }

    [Fact]
    public async Task Snapshot_RoundTrip_ReturnsGuiResult()
    {
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.snapshot", "{\"paneId\":\"p1\"}"), browserAutomation: EchoBridge());

        var resp = await BrowserAutomationCommands.Snapshot(ctx);

        Assert.True(resp.Ok, resp.Error?.Message);
        Assert.Equal("snapshot", resp.Data!.Value.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Click_MissingParams_FailsInvalid()
    {
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.click", "{\"paneId\":\"p1\"}"), browserAutomation: EchoBridge());

        var resp = await BrowserAutomationCommands.Click(ctx);

        Assert.False(resp.Ok);
        Assert.Equal("invalid_params", resp.Error!.Code);
    }

    [Fact]
    public async Task Fill_RoundTrip_Succeeds()
    {
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.fill", "{\"paneId\":\"p1\",\"ref\":\"e2\",\"value\":\"abc\"}"), browserAutomation: EchoBridge());

        var resp = await BrowserAutomationCommands.Fill(ctx);

        Assert.True(resp.Ok, resp.Error?.Message);
        Assert.Equal("fill", resp.Data!.Value.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Eval_NoGui_ReturnsAdrift()
    {
        var adrift = new BrowserAutomationBridge(_ => { }, NullLogger.Instance, TimeSpan.FromMilliseconds(60));
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.eval", "{\"paneId\":\"p1\",\"js\":\"1+1\"}"), browserAutomation: adrift);

        var resp = await BrowserAutomationCommands.Eval(ctx);

        Assert.False(resp.Ok);
        Assert.Equal("adrift", resp.Error!.Code);
    }

    [Fact]
    public async Task Screenshot_NoBridge_FailsNotReady()
    {
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.screenshot", "{\"paneId\":\"p1\"}"));

        var resp = await BrowserAutomationCommands.Screenshot(ctx);

        Assert.False(resp.Ok);
        Assert.Equal("not_ready", resp.Error!.Code);
    }

    [Fact]
    public async Task Screenshot_MissingPaneId_FailsInvalid()
    {
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.screenshot", "{}"), browserAutomation: EchoBridge());

        var resp = await BrowserAutomationCommands.Screenshot(ctx);

        Assert.False(resp.Ok);
        Assert.Equal("invalid_params", resp.Error!.Code);
    }

    [Fact]
    public async Task Screenshot_RoundTrip_ReturnsGuiResult()
    {
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.screenshot", "{\"paneId\":\"p1\"}"), browserAutomation: EchoBridge());

        var resp = await BrowserAutomationCommands.Screenshot(ctx);

        Assert.True(resp.Ok, resp.Error?.Message);
        Assert.Equal("screenshot", resp.Data!.Value.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Screenshot_NoGui_ReturnsAdrift()
    {
        var adrift = new BrowserAutomationBridge(_ => { }, NullLogger.Instance, TimeSpan.FromMilliseconds(60));
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.screenshot", "{\"paneId\":\"p1\"}"), browserAutomation: adrift);

        var resp = await BrowserAutomationCommands.Screenshot(ctx);

        Assert.False(resp.Ok);
        Assert.Equal("adrift", resp.Error!.Code);
    }

    [Fact]
    public async Task SetUserAgent_NoBridge_FailsNotReady()
    {
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.setUserAgent", "{\"paneId\":\"p1\",\"userAgent\":\"UA\"}"));

        var resp = await BrowserAutomationCommands.SetUserAgent(ctx);

        Assert.False(resp.Ok);
        Assert.Equal("not_ready", resp.Error!.Code);
    }

    [Fact]
    public async Task SetUserAgent_MissingUserAgent_FailsInvalid()
    {
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.setUserAgent", "{\"paneId\":\"p1\"}"), browserAutomation: EchoBridge());

        var resp = await BrowserAutomationCommands.SetUserAgent(ctx);

        Assert.False(resp.Ok);
        Assert.Equal("invalid_params", resp.Error!.Code);
    }

    [Fact]
    public async Task SetUserAgent_CarriesUserAgentValue()
    {
        BrowserAutomationExecEvent? emitted = null;
        BrowserAutomationBridge? bridge = null;
        bridge = new BrowserAutomationBridge(e =>
        {
            emitted = e;
            Task.Run(() => bridge!.Complete(e.RequestId, "{\"ok\":true}"));
        }, NullLogger.Instance, TimeSpan.FromSeconds(5));
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.setUserAgent", "{\"paneId\":\"p1\",\"userAgent\":\"CoveUA/1.0\"}"), browserAutomation: bridge);

        var resp = await BrowserAutomationCommands.SetUserAgent(ctx);

        Assert.True(resp.Ok, resp.Error?.Message);
        Assert.Equal("setUserAgent", emitted!.Kind);
        Assert.Equal("CoveUA/1.0", emitted.Value);
    }

    [Fact]
    public async Task Result_CompletesPending_AndRejectsUnknown()
    {
        var requestIdBox = new System.Threading.Tasks.TaskCompletionSource<string>();
        var bridge = new BrowserAutomationBridge(e => requestIdBox.TrySetResult(e.RequestId), NullLogger.Instance, TimeSpan.FromSeconds(10));
        var pending = bridge.ExecuteAsync("p1", "eval", null, null, "2+2", default);
        var requestId = await requestIdBox.Task;

        var unknownCtx = new EngineDispatchContext(Req("cove://commands/browser.automation.result", "{\"requestId\":\"missing\",\"resultJson\":\"{}\"}"), browserAutomation: bridge);
        var unknown = await BrowserAutomationCommands.Result(unknownCtx);
        Assert.False(unknown.Ok);
        Assert.Equal("not_found", unknown.Error!.Code);

        var okCtx = new EngineDispatchContext(Req("cove://commands/browser.automation.result", "{\"requestId\":\"" + requestId + "\",\"resultJson\":\"{\\\"answer\\\":4}\"}"), browserAutomation: bridge);
        var ok = await BrowserAutomationCommands.Result(okCtx);
        Assert.True(ok.Ok);

        var outcome = await pending;
        Assert.True(outcome.Ok);
        Assert.Equal("{\"answer\":4}", outcome.ResultJson);
    }

    [Fact]
    public async Task Clear_NoBridge_FailsNotReady()
    {
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.clear", "{\"paneId\":\"p1\",\"ref\":\"e1\"}"));

        var resp = await BrowserAutomationCommands.Clear(ctx);

        Assert.False(resp.Ok);
        Assert.Equal("not_ready", resp.Error!.Code);
    }

    [Fact]
    public async Task Clear_MissingRef_FailsInvalid()
    {
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.clear", "{\"paneId\":\"p1\"}"), browserAutomation: EchoBridge());

        var resp = await BrowserAutomationCommands.Clear(ctx);

        Assert.False(resp.Ok);
        Assert.Equal("invalid_params", resp.Error!.Code);
    }

    [Fact]
    public async Task Clear_RoundTrip_Succeeds()
    {
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.clear", "{\"paneId\":\"p1\",\"ref\":\"e1\"}"), browserAutomation: EchoBridge());

        var resp = await BrowserAutomationCommands.Clear(ctx);

        Assert.True(resp.Ok, resp.Error?.Message);
        Assert.Equal("clear", resp.Data!.Value.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Type_MissingText_FailsInvalid()
    {
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.type", "{\"paneId\":\"p1\",\"ref\":\"e1\"}"), browserAutomation: EchoBridge());

        var resp = await BrowserAutomationCommands.Type(ctx);

        Assert.False(resp.Ok);
        Assert.Equal("invalid_params", resp.Error!.Code);
    }

    [Fact]
    public async Task Type_CarriesTextAsValue()
    {
        var (bridge, emitted) = CapturingBridge();
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.type", "{\"paneId\":\"p1\",\"ref\":\"e1\",\"text\":\"hi\"}"), browserAutomation: bridge);

        var resp = await BrowserAutomationCommands.Type(ctx);

        Assert.True(resp.Ok, resp.Error?.Message);
        Assert.Equal("type", emitted()!.Kind);
        Assert.Equal("e1", emitted()!.Ref);
        Assert.Equal("hi", emitted()!.Value);
    }

    [Fact]
    public async Task Press_MissingKey_FailsInvalid()
    {
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.press", "{\"paneId\":\"p1\",\"ref\":\"e1\"}"), browserAutomation: EchoBridge());

        var resp = await BrowserAutomationCommands.Press(ctx);

        Assert.False(resp.Ok);
        Assert.Equal("invalid_params", resp.Error!.Code);
    }

    [Fact]
    public async Task Press_CarriesKeyAsValue()
    {
        var (bridge, emitted) = CapturingBridge();
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.press", "{\"paneId\":\"p1\",\"ref\":\"e1\",\"key\":\"Enter\"}"), browserAutomation: bridge);

        var resp = await BrowserAutomationCommands.Press(ctx);

        Assert.True(resp.Ok, resp.Error?.Message);
        Assert.Equal("press", emitted()!.Kind);
        Assert.Equal("Enter", emitted()!.Value);
    }

    [Fact]
    public async Task Select_MissingValue_FailsInvalid()
    {
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.select", "{\"paneId\":\"p1\",\"ref\":\"e1\"}"), browserAutomation: EchoBridge());

        var resp = await BrowserAutomationCommands.Select(ctx);

        Assert.False(resp.Ok);
        Assert.Equal("invalid_params", resp.Error!.Code);
    }

    [Fact]
    public async Task Select_RoundTrip_Succeeds()
    {
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.select", "{\"paneId\":\"p1\",\"ref\":\"e1\",\"value\":\"opt2\"}"), browserAutomation: EchoBridge());

        var resp = await BrowserAutomationCommands.Select(ctx);

        Assert.True(resp.Ok, resp.Error?.Message);
        Assert.Equal("select", resp.Data!.Value.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Scroll_NoBridge_FailsNotReady()
    {
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.scroll", "{\"paneId\":\"p1\",\"y\":100}"));

        var resp = await BrowserAutomationCommands.Scroll(ctx);

        Assert.False(resp.Ok);
        Assert.Equal("not_ready", resp.Error!.Code);
    }

    [Fact]
    public async Task Scroll_MissingPaneId_FailsInvalid()
    {
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.scroll", "{\"y\":100}"), browserAutomation: EchoBridge());

        var resp = await BrowserAutomationCommands.Scroll(ctx);

        Assert.False(resp.Ok);
        Assert.Equal("invalid_params", resp.Error!.Code);
    }

    [Fact]
    public async Task Scroll_WithoutRef_RoundTrips()
    {
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.scroll", "{\"paneId\":\"p1\",\"x\":0,\"y\":250}"), browserAutomation: EchoBridge());

        var resp = await BrowserAutomationCommands.Scroll(ctx);

        Assert.True(resp.Ok, resp.Error?.Message);
        Assert.Equal("scroll", resp.Data!.Value.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Scroll_CarriesCoordinatesAsValueJson()
    {
        var (bridge, emitted) = CapturingBridge();
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.scroll", "{\"paneId\":\"p1\",\"ref\":\"e4\",\"x\":10,\"y\":20}"), browserAutomation: bridge);

        var resp = await BrowserAutomationCommands.Scroll(ctx);

        Assert.True(resp.Ok, resp.Error?.Message);
        Assert.Equal("scroll", emitted()!.Kind);
        Assert.Equal("e4", emitted()!.Ref);
        Assert.Contains("\"x\":10", emitted()!.Value);
        Assert.Contains("\"y\":20", emitted()!.Value);
    }

    [Fact]
    public async Task Wait_MissingPaneId_FailsInvalid()
    {
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.wait", "{\"text\":\"Done\"}"), browserAutomation: EchoBridge());

        var resp = await BrowserAutomationCommands.Wait(ctx);

        Assert.False(resp.Ok);
        Assert.Equal("invalid_params", resp.Error!.Code);
    }

    [Fact]
    public async Task Wait_NeitherRefNorText_FailsInvalid()
    {
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.wait", "{\"paneId\":\"p1\"}"), browserAutomation: EchoBridge());

        var resp = await BrowserAutomationCommands.Wait(ctx);

        Assert.False(resp.Ok);
        Assert.Equal("invalid_params", resp.Error!.Code);
    }

    [Fact]
    public async Task Wait_WithText_CarriesTextAndTimeout()
    {
        var (bridge, emitted) = CapturingBridge();
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.wait", "{\"paneId\":\"p1\",\"text\":\"Loaded\",\"timeoutMs\":3000}"), browserAutomation: bridge);

        var resp = await BrowserAutomationCommands.Wait(ctx);

        Assert.True(resp.Ok, resp.Error?.Message);
        Assert.Equal("wait", emitted()!.Kind);
        Assert.Contains("Loaded", emitted()!.Value);
        Assert.Contains("3000", emitted()!.Value);
    }

    [Fact]
    public async Task Wait_NoGui_ReturnsAdrift()
    {
        var adrift = new BrowserAutomationBridge(_ => { }, NullLogger.Instance, TimeSpan.FromMilliseconds(60));
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.wait", "{\"paneId\":\"p1\",\"ref\":\"e1\"}"), browserAutomation: adrift);

        var resp = await BrowserAutomationCommands.Wait(ctx);

        Assert.False(resp.Ok);
        Assert.Equal("adrift", resp.Error!.Code);
    }

    [Fact]
    public async Task Get_MissingProp_FailsInvalid()
    {
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.get", "{\"paneId\":\"p1\",\"ref\":\"e1\"}"), browserAutomation: EchoBridge());

        var resp = await BrowserAutomationCommands.Get(ctx);

        Assert.False(resp.Ok);
        Assert.Equal("invalid_params", resp.Error!.Code);
    }

    [Fact]
    public async Task Get_CarriesPropAsValue()
    {
        var (bridge, emitted) = CapturingBridge();
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.get", "{\"paneId\":\"p1\",\"ref\":\"e1\",\"prop\":\"value\"}"), browserAutomation: bridge);

        var resp = await BrowserAutomationCommands.Get(ctx);

        Assert.True(resp.Ok, resp.Error?.Message);
        Assert.Equal("get", emitted()!.Kind);
        Assert.Equal("value", emitted()!.Value);
    }

    [Fact]
    public async Task Is_MissingState_FailsInvalid()
    {
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.is", "{\"paneId\":\"p1\",\"ref\":\"e1\"}"), browserAutomation: EchoBridge());

        var resp = await BrowserAutomationCommands.Is(ctx);

        Assert.False(resp.Ok);
        Assert.Equal("invalid_params", resp.Error!.Code);
    }

    [Fact]
    public async Task Is_CarriesStateAsValue()
    {
        var (bridge, emitted) = CapturingBridge();
        var ctx = new EngineDispatchContext(Req("cove://commands/browser.is", "{\"paneId\":\"p1\",\"ref\":\"e1\",\"state\":\"visible\"}"), browserAutomation: bridge);

        var resp = await BrowserAutomationCommands.Is(ctx);

        Assert.True(resp.Ok, resp.Error?.Message);
        Assert.Equal("is", emitted()!.Kind);
        Assert.Equal("visible", emitted()!.Value);
    }
}
