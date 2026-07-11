using Cove.Engine.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ProtocolResolverTests
{
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
}
