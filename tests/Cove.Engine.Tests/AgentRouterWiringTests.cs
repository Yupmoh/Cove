using System.Text.Json;
using Cove.Engine;
using Cove.Engine.Agents;
using Cove.Engine.Pty;
using Cove.Platform.Pty;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class AgentRouterWiringTests
{
    private static PaneRegistry NewPanes() => new(PtyHostFactory.Create(NullLogger.Instance), NullLogger.Instance);

    [Fact]
    public async Task PaneSpawn_WithAdapter_RegistersAgent()
    {
        var router = new AgentMessageRouter();
        var panes = NewPanes();
        var request = new ControlRequest("1", "cove://commands/pane.spawn", JsonDocument.Parse("""{"command":"/bin/sh","args":["-c","sleep 30"],"adapter":"claude-code","agentName":"Researcher","workspace":"ws1","room":"room1"}""").RootElement);
        var response = await EngineCommandRouter.RouteAsync(request, panes: panes, agentRouter: router);

        Assert.True(response!.Ok);
        var agents = router.List("all").ToList();
        Assert.Single(agents);
        Assert.Equal("claude-code", agents[0].Adapter);
        Assert.Equal("Researcher", agents[0].Name);
        Assert.Equal("ws1", agents[0].Workspace);
        Assert.Equal("room1", agents[0].Room);
    }

    [Fact]
    public async Task PaneSpawn_WithoutAdapter_DoesNotRegister()
    {
        var router = new AgentMessageRouter();
        var panes = NewPanes();
        var request = new ControlRequest("1", "cove://commands/pane.spawn", JsonDocument.Parse("""{"command":"/bin/sh","args":["-c","sleep 30"]}""").RootElement);
        var response = await EngineCommandRouter.RouteAsync(request, panes: panes, agentRouter: router);

        Assert.True(response!.Ok);
        Assert.Empty(router.List("all"));
    }

    [Fact]
    public async Task PaneKill_UnregistersAgent()
    {
        var router = new AgentMessageRouter();
        var panes = NewPanes();
        var spawnReq = new ControlRequest("1", "cove://commands/pane.spawn", JsonDocument.Parse("""{"command":"/bin/sh","args":["-c","sleep 30"],"adapter":"claude-code"}""").RootElement);
        var spawnResp = await EngineCommandRouter.RouteAsync(spawnReq, panes: panes, agentRouter: router);
        var paneId = spawnResp!.Data!.Value.GetProperty("paneId").GetString()!;
        Assert.Single(router.List("all"));

        var killReq = new ControlRequest("2", "cove://commands/pane.kill", JsonDocument.Parse($"{{\"paneId\":\"{paneId}\"}}").RootElement);
        var killResp = await EngineCommandRouter.RouteAsync(killReq, panes: panes, agentRouter: router);

        Assert.True(killResp!.Ok);
        Assert.Empty(router.List("all"));
    }

    [Fact]
    public async Task AgentList_ReturnsRegisteredAgents()
    {
        var router = new AgentMessageRouter();
        var panes = NewPanes();
        var spawnReq = new ControlRequest("1", "cove://commands/pane.spawn", JsonDocument.Parse("""{"command":"/bin/sh","args":["-c","sleep 30"],"adapter":"claude-code","agentName":"A","workspace":"ws1","room":"room1"}""").RootElement);
        await EngineCommandRouter.RouteAsync(spawnReq, panes: panes, agentRouter: router);

        var listReq = new ControlRequest("2", "cove://commands/agent.list", JsonDocument.Parse("""{"scope":"all"}""").RootElement);
        var listResp = await EngineCommandRouter.RouteAsync(listReq, panes: panes, agentRouter: router);

        Assert.True(listResp!.Ok);
        var agents = listResp.Data!.Value.GetProperty("agents");
        Assert.Equal(1, agents.GetArrayLength());
        Assert.Equal("claude-code", agents[0].GetProperty("adapter").GetString());
        Assert.Equal("A", agents[0].GetProperty("name").GetString());
    }
}
