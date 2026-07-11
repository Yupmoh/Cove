using System.Text.Json;
using Cove.Engine.Agents;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class AgentMessageFramerTests
{
    [Fact]
    public void Frame_IncludesSenderIdentityAndAdapterAndReply()
    {
        var sender = new AgentMessageSender("p1", "claude-code", "Researcher");
        var framed = AgentMessageFramer.Frame(sender, "hello there", replyPrefix: "p1");
        Assert.Contains("Message from", framed);
        Assert.Contains("Researcher", framed);
        Assert.Contains("claude-code", framed);
        Assert.Contains("cove agent message p1", framed);
        Assert.Contains("hello there", framed);
    }

    [Fact]
    public void Frame_NewlinesPreserved()
    {
        var sender = new AgentMessageSender("p1", "claude-code", "Researcher");
        var framed = AgentMessageFramer.Frame(sender, "line1\nline2", replyPrefix: "p1");
        Assert.Contains("line1\nline2", framed);
    }

    [Fact]
    public void NoFrame_ReturnsRawBody()
    {
        var body = "hello there";
        var raw = AgentMessageFramer.NoFrame(body);
        Assert.Equal(body, raw);
    }

    [Fact]
    public void Frame_AnonymousSender_UsesNookId()
    {
        var sender = new AgentMessageSender("p1", "claude-code", null);
        var framed = AgentMessageFramer.Frame(sender, "hi", replyPrefix: "p1");
        Assert.Contains("p1", framed);
    }
}

public sealed class AgentMessageRouterTests
{
    [Fact]
    public void ResolveTarget_FullNookId_Matches()
    {
        var router = new AgentMessageRouter();
        router.Register("p1abc123", "claude-code", "Researcher");
        var target = router.ResolveTarget("p1abc123");
        Assert.NotNull(target);
        Assert.Equal("p1abc123", target!.NookId);
    }

    [Fact]
    public void ResolveTarget_PrefixMatchesUnique()
    {
        var router = new AgentMessageRouter();
        router.Register("p1abc123", "claude-code", "Researcher");
        router.Register("p2def456", "codex", "Writer");
        var target = router.ResolveTarget("p1");
        Assert.NotNull(target);
        Assert.Equal("p1abc123", target!.NookId);
    }

    [Fact]
    public void ResolveTarget_PrefixAmbiguous_ReturnsNull()
    {
        var router = new AgentMessageRouter();
        router.Register("p1abc", "claude-code", "A");
        router.Register("p1def", "codex", "B");
        var target = router.ResolveTarget("p1");
        Assert.Null(target);
    }

    [Fact]
    public void ResolveTarget_NoMatch_ReturnsNull()
    {
        var router = new AgentMessageRouter();
        router.Register("p1abc", "claude-code", "A");
        var target = router.ResolveTarget("nonexistent");
        Assert.Null(target);
    }

    [Fact]
    public void Unregister_RemovesAgent()
    {
        var router = new AgentMessageRouter();
        router.Register("p1abc", "claude-code", "A");
        router.Unregister("p1abc");
        Assert.Null(router.ResolveTarget("p1abc"));
    }

    [Fact]
    public void List_ReturnsAllRegisteredAgents()
    {
        var router = new AgentMessageRouter();
        router.Register("p1abc", "claude-code", "A");
        router.Register("p2def", "codex", "B");
        var agents = router.List().ToList();
        Assert.Equal(2, agents.Count);
        Assert.Contains(agents, a => a.NookId == "p1abc" && a.Adapter == "claude-code" && a.Name == "A");
        Assert.Contains(agents, a => a.NookId == "p2def" && a.Adapter == "codex" && a.Name == "B");
    }

    [Fact]
    public void List_RespectsMcpVisibleFalse_HidesAgent()
    {
        var router = new AgentMessageRouter();
        router.Register("p1abc", "claude-code", "A", mcpVisible: false);
        router.Register("p2def", "codex", "B", mcpVisible: true);
        var agents = router.List().ToList();
        Assert.Single(agents);
        Assert.Equal("p2def", agents[0].NookId);
    }

    [Fact]
    public void List_ScopeSameBay_FiltersByBay()
    {
        var router = new AgentMessageRouter();
        router.Register("p1abc", "claude-code", "A", bay: "ws1");
        router.Register("p2def", "codex", "B", bay: "ws2");
        var agents = router.List(scope: "same-bay", requesterBay: "ws1").ToList();
        Assert.Single(agents);
        Assert.Equal("p1abc", agents[0].NookId);
    }

    [Fact]
    public void List_ScopeSameTab_FiltersByShoreExcludingSelf()
    {
        var router = new AgentMessageRouter();
        router.Register("p1abc", "claude-code", "A", shore: "shore1");
        router.Register("p2def", "codex", "B", shore: "shore1");
        router.Register("p3ghi", "gemini", "C", shore: "shore2");
        var agents = router.List(scope: "same-tab", requesterNookId: "p1abc", requesterShore: "shore1").ToList();
        Assert.Single(agents);
        Assert.Equal("p2def", agents[0].NookId);
    }
}
