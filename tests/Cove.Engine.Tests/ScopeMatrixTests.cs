using System.Text.Json;
using Cove.Engine.Knowledge;
using Cove.Engine.Protocol;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Cove.Testing;

namespace Cove.Engine.Tests;

public sealed class ScopeMatrixTests
{
    private static string NewDir()
        => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-scope-matrix-" + System.Guid.NewGuid().ToString("N"));

    private static Cove.Engine.Pty.NookRegistry NewNooks()
        => new(Cove.Platform.Pty.PtyHostFactory.Create(NullLogger.Instance), NullLogger.Instance);

    private static string SpawnNook(Cove.Engine.Pty.NookRegistry nooks)
    {
        var request = new ControlRequest(
            "spawn",
            "cove://commands/nook.spawn",
            JsonDocument.Parse("""{"command":"/bin/sh","args":["-c","sleep 30"]}""").RootElement.Clone());
        var response = EngineCommandRouter.RouteAsync(request, nooks: nooks).GetAwaiter().GetResult();
        return response!.Data!.Value.GetProperty("nookId").GetString()!;
    }

    [Fact]
    public async Task CanvasAction_CoveCommand_RedrivePreservesSourceAndCallerNookId()
    {
        var parameters = JsonDocument.Parse("""
            {
              "action": "cove_command",
              "uri": "cove://commands/knowledge.ping",
              "actionId": "action-1",
              "state": { "echo": "hello" }
            }
            """).RootElement.Clone();
        var request = new ControlRequest(
            "request-1",
            "cove://commands/canvas.action",
            parameters,
            Source: "mcp",
            CallerNookId: "caller-nook");
        var context = new EngineDispatchContext(request);
        ControlRequest? redriven = null;
        context.Redrive = subRequest =>
        {
            redriven = subRequest;
            return Task.FromResult<ControlResponse?>(new ControlResponse(subRequest.Id, true));
        };

        var response = await KnowledgeCommands.CanvasAction(context);

        Assert.True(response.Ok);
        Assert.NotNull(redriven);
        Assert.Equal(request.Source, redriven!.Source);
        Assert.Equal(request.CallerNookId, redriven.CallerNookId);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task ExecuteCommand_ForeignNook_PreservesCallerScopeWhileAnonymousStillPasses()
    {
        using var nooks = NewNooks();
        var scopeStore = new NookScopeStore(NewDir(), NullLogger.Instance);
        string callerNook = "", targetNook = "";
        try
        {
            callerNook = SpawnNook(nooks);
            targetNook = SpawnNook(nooks);
            scopeStore.SetScope(callerNook, McpScope.SameTab);
            var parameters = JsonDocument.Parse($$"""
                {
                  "command": "cove://commands/nook.write",
                  "params": { "nookId": "{{targetNook}}", "dataBase64": "aGk=" }
                }
                """).RootElement.Clone();

            var identified = await EngineCommandRouter.RouteAsync(
                new ControlRequest("identified", "cove://commands/execute_command", parameters, CallerNookId: callerNook),
                nooks: nooks,
                nookScopes: scopeStore);
            var anonymous = await EngineCommandRouter.RouteAsync(
                new ControlRequest("anonymous", "cove://commands/execute_command", parameters),
                nooks: nooks,
                nookScopes: scopeStore);

            Assert.NotNull(identified);
            Assert.False(identified!.Ok);
            Assert.Equal("access_denied", identified.Error?.Code);
            Assert.NotNull(anonymous);
            Assert.True(anonymous!.Ok);
        }
        finally
        {
            if (!string.IsNullOrEmpty(callerNook)) nooks.Kill(callerNook);
            if (!string.IsNullOrEmpty(targetNook)) nooks.Kill(targetNook);
        }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task CanvasSendToAgent_ForeignNook_DeniesIdentifiedCallerWhileAnonymousStillPasses()
    {
        using var nooks = NewNooks();
        var scopeStore = new NookScopeStore(NewDir(), NullLogger.Instance);
        string callerNook = "", targetNook = "";
        try
        {
            callerNook = SpawnNook(nooks);
            targetNook = SpawnNook(nooks);
            scopeStore.SetScope(callerNook, McpScope.SameTab);
            var parameters = JsonDocument.Parse($$"""
                {
                  "action": "send_to_agent",
                  "targetNook": "{{targetNook}}",
                  "actionId": "action-1",
                  "payload": "hello"
                }
                """).RootElement.Clone();

            var identified = await EngineCommandRouter.RouteAsync(
                new ControlRequest("identified", "cove://commands/canvas.action", parameters, CallerNookId: callerNook),
                nooks: nooks,
                nookScopes: scopeStore);
            var anonymous = await EngineCommandRouter.RouteAsync(
                new ControlRequest("anonymous", "cove://commands/canvas.action", parameters),
                nooks: nooks,
                nookScopes: scopeStore);

            Assert.NotNull(identified);
            Assert.False(identified!.Ok);
            Assert.Equal("access_denied", identified.Error?.Code);
            Assert.NotNull(anonymous);
            Assert.True(anonymous!.Ok);
        }
        finally
        {
            if (!string.IsNullOrEmpty(callerNook)) nooks.Kill(callerNook);
            if (!string.IsNullOrEmpty(targetNook)) nooks.Kill(targetNook);
        }
    }

    [Fact]
    public async Task AgentMessage_ForeignNook_DeniesIdentifiedCallerWhileAnonymousReachesHandler()
    {
        var scopeStore = new NookScopeStore(NewDir(), NullLogger.Instance);
        scopeStore.SetScope("caller-nook", McpScope.SameTab);
        var parameters = JsonDocument.Parse(
            """{"target":"foreign-nook","body":"hello","fromNookId":null,"fromAdapter":null,"fromName":null,"noFrame":true,"submitPauseMs":null}""")
            .RootElement.Clone();

        var identified = await EngineCommandRouter.RouteAsync(
            new ControlRequest("identified", "cove://commands/agent.message", parameters, CallerNookId: "caller-nook"),
            nookScopes: scopeStore);
        var anonymous = await EngineCommandRouter.RouteAsync(
            new ControlRequest("anonymous", "cove://commands/agent.message", parameters),
            nookScopes: scopeStore);

        Assert.NotNull(identified);
        Assert.False(identified!.Ok);
        Assert.Equal("access_denied", identified.Error?.Code);
        Assert.NotNull(anonymous);
        Assert.False(anonymous!.Ok);
        Assert.Equal("not_ready", anonymous.Error?.Code);
    }

    [Fact]
    public async Task BrowserClick_ForeignNook_DeniesIdentifiedCallerWhileAnonymousReachesHandler()
    {
        var scopeStore = new NookScopeStore(NewDir(), NullLogger.Instance);
        scopeStore.SetScope("caller-nook", McpScope.SameTab);
        var parameters = JsonDocument.Parse("""{"nookId":"foreign-nook","ref":"e1"}""").RootElement.Clone();

        var identified = await EngineCommandRouter.RouteAsync(
            new ControlRequest("identified", "cove://commands/browser.click", parameters, CallerNookId: "caller-nook"),
            nookScopes: scopeStore);
        var anonymous = await EngineCommandRouter.RouteAsync(
            new ControlRequest("anonymous", "cove://commands/browser.click", parameters),
            nookScopes: scopeStore);

        Assert.NotNull(identified);
        Assert.False(identified!.Ok);
        Assert.Equal("access_denied", identified.Error?.Code);
        Assert.NotNull(anonymous);
        Assert.False(anonymous!.Ok);
        Assert.Equal("not_ready", anonymous.Error?.Code);
    }
}
