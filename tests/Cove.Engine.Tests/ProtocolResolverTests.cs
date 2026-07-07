using Cove.Engine.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ProtocolResolverTests
{
    [Fact]
    public void Resolve_CommandsAction_MapsToEngineUri()
    {
        var resolver = new ProtocolResolver();
        var (uri, params_) = resolver.Resolve("cove://commands/pane.split.horizontal?pane=$FOCUS", focusedPaneId: "p1", activeRoomId: "room1");
        Assert.Equal("cove://commands/layout.mutate", uri);
    }

    [Fact]
    public void Resolve_FocusSigil_ExpandsToFocusedPaneId()
    {
        var resolver = new ProtocolResolver();
        var (uri, params_) = resolver.Resolve("cove://commands/pane.close?pane=$FOCUS", focusedPaneId: "p1", activeRoomId: null);
        Assert.Contains("p1", params_?.ToString() ?? "");
    }

    [Fact]
    public void Resolve_ActiveSigil_ExpandsToActiveRoomId()
    {
        var resolver = new ProtocolResolver();
        var (uri, params_) = resolver.Resolve("cove://commands/room.switch?roomId=$ACTIVE", focusedPaneId: null, activeRoomId: "room1");
        Assert.Contains("room1", params_?.ToString() ?? "");
    }

    [Fact]
    public void Resolve_PanesCategory_MapsToPaneCommands()
    {
        var resolver = new ProtocolResolver();
        var (uri, params_) = resolver.Resolve("cove://panes/", focusedPaneId: null, activeRoomId: null);
        Assert.Equal("cove://commands/pane.list", uri);
    }

    [Fact]
    public void Resolve_PanesWrite_MapsToPaneWrite()
    {
        var resolver = new ProtocolResolver();
        var (uri, params_) = resolver.Resolve("cove://panes/p1/write", focusedPaneId: null, activeRoomId: null);
        Assert.Equal("cove://commands/pane.write", uri);
    }

    [Fact]
    public void Resolve_AgentsMessage_MapsToAgentMessage()
    {
        var resolver = new ProtocolResolver();
        var (uri, params_) = resolver.Resolve("cove://agents/p1/message", focusedPaneId: null, activeRoomId: null);
        Assert.Equal("cove://commands/agent.message", uri);
    }

    [Fact]
    public void Resolve_AgentsList_MapsToAgentList()
    {
        var resolver = new ProtocolResolver();
        var (uri, params_) = resolver.Resolve("cove://agents/list", focusedPaneId: null, activeRoomId: null);
        Assert.Equal("cove://commands/agent.list", uri);
    }

    [Fact]
    public void Resolve_SkillsIndex_MapsToSkillsIndex()
    {
        var resolver = new ProtocolResolver();
        var (uri, params_) = resolver.Resolve("cove://skills/index", focusedPaneId: null, activeRoomId: null);
        Assert.Equal("cove://commands/skills.index", uri);
    }

    [Fact]
    public void Resolve_ExplicitPaneId_NotSigil_PassedThrough()
    {
        var resolver = new ProtocolResolver();
        var (uri, params_) = resolver.Resolve("cove://commands/pane.close?pane=explicit-id", focusedPaneId: "p1", activeRoomId: null);
        Assert.Contains("explicit-id", params_?.ToString() ?? "");
    }

    [Fact]
    public void Resolve_UnknownCategory_ReturnsNull()
    {
        var resolver = new ProtocolResolver();
        var (uri, params_) = resolver.Resolve("cove://unknown/action", focusedPaneId: null, activeRoomId: null);
        Assert.Null(uri);
    }
}
