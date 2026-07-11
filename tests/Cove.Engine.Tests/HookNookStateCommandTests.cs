using Cove.Engine;
using Cove.Engine.Hooks;
using Cove.Protocol;
using Xunit;

public class HookNookStateCommandTests
{
    [Fact]
    public async Task NookStates_ReturnsAllTrackedNooks()
    {
        var router = new HookEventRouter();
        router.Route(new Cove.Adapters.HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" });
        router.Route(new Cove.Adapters.HookEvent { Adapter = "codex", Event = "session-start", NookId = "p2" });

        var request = new ControlRequest("1", "cove://hooks/nook-states");
        var response = await EngineCommandRouter.RouteAsync(request, hookRouter: router);

        Assert.NotNull(response);
        Assert.True(response!.Ok);
        var nooks = response.Data!.Value.GetProperty("nooks");
        Assert.Equal(2, nooks.GetArrayLength());
    }

    [Fact]
    public async Task NookStates_WithoutRouter_ReturnsNotReady()
    {
        var request = new ControlRequest("1", "cove://hooks/nook-states");
        var response = await EngineCommandRouter.RouteAsync(request);

        Assert.NotNull(response);
        Assert.False(response!.Ok);
        Assert.Equal("not_ready", response.Error!.Code);
    }
}
