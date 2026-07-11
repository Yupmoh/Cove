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
    private static NookRegistry NewNooks() => new(PtyHostFactory.Create(NullLogger.Instance), NullLogger.Instance);

    [Fact]
    public async Task NookSpawn_WithAdapter_RegistersAgent()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var router = new AgentMessageRouter();
        var nooks = NewNooks();
        var request = new ControlRequest("1", "cove://commands/nook.spawn", JsonDocument.Parse("""{"command":"/bin/sh","args":["-c","sleep 30"],"adapter":"claude-code","agentName":"Researcher","bay":"ws1","shore":"shore1"}""").RootElement);
        var response = await EngineCommandRouter.RouteAsync(request, nooks: nooks, agentRouter: router);

        Assert.True(response!.Ok);
        var agents = router.List("all").ToList();
        Assert.Single(agents);
        Assert.Equal("claude-code", agents[0].Adapter);
        Assert.Equal("Researcher", agents[0].Name);
        Assert.Equal("ws1", agents[0].Bay);
        Assert.Equal("shore1", agents[0].Shore);
    }

    [Fact]
    public async Task NookSpawn_WithoutAdapter_DoesNotRegister()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var router = new AgentMessageRouter();
        var nooks = NewNooks();
        var request = new ControlRequest("1", "cove://commands/nook.spawn", JsonDocument.Parse("""{"command":"/bin/sh","args":["-c","sleep 30"]}""").RootElement);
        var response = await EngineCommandRouter.RouteAsync(request, nooks: nooks, agentRouter: router);

        Assert.True(response!.Ok);
        Assert.Empty(router.List("all"));
    }

    [Fact]
    public async Task NookKill_UnregistersAgent()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var router = new AgentMessageRouter();
        var nooks = NewNooks();
        var spawnReq = new ControlRequest("1", "cove://commands/nook.spawn", JsonDocument.Parse("""{"command":"/bin/sh","args":["-c","sleep 30"],"adapter":"claude-code"}""").RootElement);
        var spawnResp = await EngineCommandRouter.RouteAsync(spawnReq, nooks: nooks, agentRouter: router);
        var nookId = spawnResp!.Data!.Value.GetProperty("nookId").GetString()!;
        Assert.Single(router.List("all"));

        var killReq = new ControlRequest("2", "cove://commands/nook.kill", JsonDocument.Parse($"{{\"nookId\":\"{nookId}\"}}").RootElement);
        var killResp = await EngineCommandRouter.RouteAsync(killReq, nooks: nooks, agentRouter: router);

        Assert.True(killResp!.Ok);
        Assert.Empty(router.List("all"));
    }

    [Fact]
    public async Task AgentList_ReturnsRegisteredAgents()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var router = new AgentMessageRouter();
        var nooks = NewNooks();
        var spawnReq = new ControlRequest("1", "cove://commands/nook.spawn", JsonDocument.Parse("""{"command":"/bin/sh","args":["-c","sleep 30"],"adapter":"claude-code","agentName":"A","bay":"ws1","shore":"shore1"}""").RootElement);
        await EngineCommandRouter.RouteAsync(spawnReq, nooks: nooks, agentRouter: router);

        var listReq = new ControlRequest("2", "cove://commands/agent.list", JsonDocument.Parse("""{"scope":"all"}""").RootElement);
        var listResp = await EngineCommandRouter.RouteAsync(listReq, nooks: nooks, agentRouter: router);

        Assert.True(listResp!.Ok);
        var agents = listResp.Data!.Value.GetProperty("agents");
        Assert.Equal(1, agents.GetArrayLength());
        Assert.Equal("claude-code", agents[0].GetProperty("adapter").GetString());
        Assert.Equal("A", agents[0].GetProperty("name").GetString());
    }
}
