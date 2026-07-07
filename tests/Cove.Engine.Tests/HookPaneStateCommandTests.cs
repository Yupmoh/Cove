using Cove.Engine;
using Cove.Engine.Hooks;
using Cove.Protocol;
using Xunit;

public class HookPaneStateCommandTests
{
    [Fact]
    public async Task PaneStates_ReturnsAllTrackedPanes()
    {
        var router = new HookEventRouter();
        router.Route(new Cove.Adapters.HookEvent { Adapter = "claude-code", Event = "session-start", PaneId = "p1" });
        router.Route(new Cove.Adapters.HookEvent { Adapter = "codex", Event = "session-start", PaneId = "p2" });

        var request = new ControlRequest("1", "cove://hooks/pane-states");
        var response = await EngineCommandRouter.RouteAsync(request, hookRouter: router);

        Assert.NotNull(response);
        Assert.True(response!.Ok);
        var panes = response.Data!.Value.GetProperty("panes");
        Assert.Equal(2, panes.GetArrayLength());
    }

    [Fact]
    public async Task PaneStates_WithoutRouter_ReturnsNotReady()
    {
        var request = new ControlRequest("1", "cove://hooks/pane-states");
        var response = await EngineCommandRouter.RouteAsync(request);

        Assert.NotNull(response);
        Assert.False(response!.Ok);
        Assert.Equal("not_ready", response.Error!.Code);
    }
}
