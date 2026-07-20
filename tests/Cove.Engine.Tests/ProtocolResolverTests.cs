using Cove.Engine.Protocol;
using Cove.Engine.Sessions;
using Cove.Generated;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ProtocolResolverTests
{
    [Theory]
    [InlineData("cove://commands/nook.split.horizontal")]
    [InlineData("cove://commands/nook.split.vertical")]
    [InlineData("cove://commands/nook.close")]
    [InlineData("cove://commands/shore.new")]
    [InlineData("cove://nooks")]
    [InlineData("cove://nooks/p1/write")]
    [InlineData("cove://nooks/p1/scrollback")]
    [InlineData("cove://agents/list")]
    [InlineData("cove://agents/p1/message")]
    [InlineData("cove://agents/p1/dismiss")]
    [InlineData("cove://agents/p1/wake")]
    [InlineData("cove://skills/index")]
    public void AdvertisedAliases_ResolveToRegisteredCommands(
        string alias)
    {
        var resolver = new ProtocolResolver();
        var (uri, _) = resolver.Resolve(alias, "p1", "shore-1");

        Assert.NotNull(uri);
        Assert.Contains(uri!, CoveCommandRegistry.Handlers.Keys);
    }

    [Fact]
    public void Resolve_CommandsAction_MapsToEngineUri()
    {
        var resolver = new ProtocolResolver();
        var (uri, params_) = resolver.Resolve("cove://commands/nook.split.horizontal?nook=$FOCUS", focusedNookId: "p1", activeShoreId: "shore1");
        Assert.Equal("cove://commands/layout.mutate", uri);
    }

    [Fact]
    public void Resolve_FocusSigil_ExpandsToFocusedNookId()
    {
        var resolver = new ProtocolResolver();
        var (uri, params_) = resolver.Resolve("cove://commands/nook.close?nook=$FOCUS", focusedNookId: "p1", activeShoreId: null);
        Assert.Contains("p1", params_?.ToString() ?? "");
    }

    [Fact]
    public void Resolve_ActiveSigil_ExpandsToActiveShoreId()
    {
        var resolver = new ProtocolResolver();
        var (uri, params_) = resolver.Resolve("cove://commands/shore.switch?shoreId=$ACTIVE", focusedNookId: null, activeShoreId: "shore1");
        Assert.Contains("shore1", params_?.ToString() ?? "");
    }

    [Fact]
    public void Resolve_NooksCategory_MapsToNookCommands()
    {
        var resolver = new ProtocolResolver();
        var (uri, params_) = resolver.Resolve("cove://nooks/", focusedNookId: null, activeShoreId: null);
        Assert.Equal("cove://commands/nook.list", uri);
    }

    [Fact]
    public void Resolve_NooksWrite_MapsToNookWrite()
    {
        var resolver = new ProtocolResolver();
        var (uri, params_) = resolver.Resolve("cove://nooks/p1/write", focusedNookId: null, activeShoreId: null);
        Assert.Equal("cove://commands/nook.write", uri);
    }

    [Fact]
    public void Resolve_NooksScrollback_MapsToRegisteredRead()
    {
        var resolver = new ProtocolResolver();
        var (uri, parameters) = resolver.Resolve(
            "cove://nooks/p1/scrollback",
            focusedNookId: null,
            activeShoreId: null);
        Assert.Equal("cove://commands/nook.read", uri);
        Assert.Equal(
            "p1",
            parameters?.GetProperty("nookId").GetString());
    }

    [Fact]
    public void Resolve_AgentsMessage_MapsToAgentMessage()
    {
        var resolver = new ProtocolResolver();
        var (uri, params_) = resolver.Resolve("cove://agents/p1/message", focusedNookId: null, activeShoreId: null);
        Assert.Equal("cove://commands/agent.message", uri);
    }

    [Fact]
    public void Resolve_AgentsList_MapsToAgentList()
    {
        var resolver = new ProtocolResolver();
        var (uri, params_) = resolver.Resolve("cove://agents/list", focusedNookId: null, activeShoreId: null);
        Assert.Equal("cove://commands/agent.list", uri);
    }

    [Fact]
    public void Resolve_AgentsWake_MapsToSessionForeground()
    {
        var resolver = new ProtocolResolver();
        var (uri, parameters) = resolver.Resolve(
            "cove://agents/p1/wake",
            focusedNookId: null,
            activeShoreId: null);
        Assert.Equal(
            "cove://commands/session.foreground",
            uri);
        Assert.Equal(
            "p1",
            parameters?.GetProperty("nookId").GetString());
    }

    [Fact]
    public void Resolve_AgentsDismiss_UsesNookReferenceShape()
    {
        var resolver = new ProtocolResolver();
        var (_, parameters) = resolver.Resolve(
            "cove://agents/p1/dismiss",
            focusedNookId: null,
            activeShoreId: null);
        Assert.Equal(
            "p1",
            parameters?.GetProperty("nookId").GetString());
        Assert.False(
            parameters?.TryGetProperty(
                "target",
                out _) ?? false);
    }

    [Fact]
    public void Resolve_CloseAlias_UsesNookReferenceShape()
    {
        var resolver = new ProtocolResolver();
        var (_, parameters) = resolver.Resolve(
            "cove://commands/nook.close?nook=p1",
            focusedNookId: null,
            activeShoreId: null);
        Assert.Equal(
            "p1",
            parameters?.GetProperty("nookId").GetString());
    }

    [Fact]
    public void Resolve_UnsupportedLauncherAlias_IsNotAdvertised()
    {
        var resolver = new ProtocolResolver();
        var (uri, _) = resolver.Resolve(
            "cove://commands/launcher.open",
            focusedNookId: null,
            activeShoreId: null);
        Assert.Null(uri);
    }

    [Fact]
    public void Resolve_SkillsIndex_MapsToSkillsIndex()
    {
        var resolver = new ProtocolResolver();
        var (uri, params_) = resolver.Resolve("cove://skills/index", focusedNookId: null, activeShoreId: null);
        Assert.Equal("cove://commands/skills.index", uri);
    }

    [Fact]
    public void Resolve_ExplicitNookId_NotSigil_PassedThrough()
    {
        var resolver = new ProtocolResolver();
        var (uri, params_) = resolver.Resolve("cove://commands/nook.close?nook=explicit-id", focusedNookId: "p1", activeShoreId: null);
        Assert.Contains("explicit-id", params_?.ToString() ?? "");
    }

    [Fact]
    public void Resolve_UnknownCategory_ReturnsNull()
    {
        var resolver = new ProtocolResolver();
        var (uri, params_) = resolver.Resolve("cove://unknown/action", focusedNookId: null, activeShoreId: null);
        Assert.Null(uri);
    }

    [Fact]
    public async Task ProtocolResolve_RejectsUnregisteredCommand()
    {
        var parameters = System.Text.Json.JsonSerializer
            .SerializeToElement(
                new ProtocolResolveParams(
                    "cove://commands/does.not.exist",
                    null,
                    null),
                CoveJsonContext.Default.ProtocolResolveParams);
        var response = await EngineCommandRouter.RouteAsync(
            new ControlRequest(
                "resolve",
                "cove://commands/protocol.resolve",
                parameters));

        Assert.False(response!.Ok);
        Assert.Equal(
            "unsupported_alias",
            response.Error!.Code);
    }

    [Fact]
    public async Task WakeAlias_ForegroundsOnceAndRejectsNoOp()
    {
        var sessions = new SessionResumeOrchestrator();
        sessions.Register(
            "p1",
            "omp",
            "session-1");
        sessions.Background("p1");
        var resolver = new ProtocolResolver();
        var (uri, parameters) = resolver.Resolve(
            "cove://agents/p1/wake",
            null,
            null);

        var first = await EngineCommandRouter.RouteAsync(
            new ControlRequest("wake-1", uri!, parameters),
            sessions: sessions);
        var second = await EngineCommandRouter.RouteAsync(
            new ControlRequest("wake-2", uri!, parameters),
            sessions: sessions);

        Assert.True(first!.Ok);
        Assert.False(second!.Ok);
        Assert.Equal("invalid_state", second.Error!.Code);
    }
}
