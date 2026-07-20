using System.Text.Json;
using Cove.Engine.Agents;
using Cove.Engine.Pty;
using Cove.Platform.Pty;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Cove.Testing;

namespace Cove.Engine.Tests;

public sealed class AgentRouterWiringTests
{
    private static NookRegistry NewNooks() => new(PtyHostFactory.Create(NullLogger.Instance), NullLogger.Instance);

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task NookSpawn_WithAdapter_RegistersAgent()
    {
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

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task NookSpawn_WithoutAdapter_DoesNotRegister()
    {
        var router = new AgentMessageRouter();
        var nooks = NewNooks();
        var request = new ControlRequest("1", "cove://commands/nook.spawn", JsonDocument.Parse("""{"command":"/bin/sh","args":["-c","sleep 30"]}""").RootElement);
        var response = await EngineCommandRouter.RouteAsync(request, nooks: nooks, agentRouter: router);

        Assert.True(response!.Ok);
        Assert.Empty(router.List("all"));
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task NookKill_UnregistersAgent()
    {
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

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task AgentList_ReturnsRegisteredAgents()
    {
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

    [Fact]
    public async Task AgentList_DerivesSameTabFromCallerIdentity()
    {
        var router = new AgentMessageRouter();
        router.Register(
            "caller",
            "omp",
            "Caller",
            "bay-1",
            "shore-1");
        router.Register(
            "peer",
            "claude-code",
            "Peer",
            "bay-1",
            "shore-1");
        router.Register(
            "foreign",
            "codex",
            "Foreign",
            "bay-1",
            "shore-2");
        var parameters = JsonSerializer.SerializeToElement(
            new AgentListParams("same-tab"),
            CoveJsonContext.Default.AgentListParams);

        var response = await EngineCommandRouter.RouteAsync(
            new ControlRequest(
                "list",
                "cove://commands/agent.list",
                parameters,
                CallerNookId: "caller"),
            agentRouter: router);

        Assert.True(response!.Ok);
        var agents = response.Data!.Value.GetProperty("agents");
        Assert.Single(agents.EnumerateArray());
        Assert.Equal(
            "peer",
            agents[0].GetProperty("nookId").GetString());
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task AgentMessage_DerivesFramedSenderFromCaller()
    {
        using var nooks = NewNooks();
        var target = nooks.Spawn(new SpawnParams(
            "/bin/sh",
            ["-c", "cat"],
            "/tmp"));
        var router = new AgentMessageRouter();
        router.Register(
            "caller",
            "omp",
            "Caller",
            "bay-1",
            "shore-1");
        router.Register(
            target.NookId,
            "claude-code",
            "Peer",
            "bay-1",
            "shore-1");
        var parameters = JsonSerializer.SerializeToElement(
            new AgentMessageParams(
                target.NookId,
                "hello",
                "forged-nook",
                "codex",
                "Forged",
                false,
                0),
            CoveJsonContext.Default.AgentMessageParams);

        var response = await EngineCommandRouter.RouteAsync(
            new ControlRequest(
                "message",
                "cove://commands/agent.message",
                parameters,
                CallerNookId: "caller"),
            nooks: nooks,
            agentRouter: router);
        await Task.Delay(100);
        var output = System.Text.Encoding.UTF8.GetString(
            nooks.Read(target.NookId, 0, 65536));

        Assert.True(response!.Ok);
        Assert.Contains(
            "Message from Caller (omp)",
            output);
        Assert.Contains(
            "cove agent message caller",
            output);
        nooks.Kill(target.NookId);
    }
}
