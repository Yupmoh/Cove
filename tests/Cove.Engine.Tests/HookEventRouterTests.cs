using System.Collections.Concurrent;
using Cove.Adapters;
using Cove.Engine.Hooks;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class HookEventRouterTests
{
    [Fact]
    public void Route_SessionStart_CreatesPaneState()
    {
        var router = new HookEventRouter();
        var ev = new HookEvent { Adapter = "claude-code", Event = "session-start", PaneId = "p1" };
        router.Route(ev);

        var state = router.GetPaneState("p1");
        Assert.NotNull(state);
        Assert.Equal("claude-code", state!.Adapter);
        Assert.Equal("active", state.Status);
    }

    [Fact]
    public void Route_SessionEnd_MarksPaneIdle()
    {
        var router = new HookEventRouter();
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", PaneId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-end", PaneId = "p1" });

        var state = router.GetPaneState("p1");
        Assert.NotNull(state);
        Assert.Equal("idle", state!.Status);
    }

    [Fact]
    public void Route_Stop_MarksPaneNeedsInput()
    {
        var router = new HookEventRouter();
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", PaneId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "stop", PaneId = "p1" });

        var state = router.GetPaneState("p1");
        Assert.NotNull(state);
        Assert.Equal("needs-input", state!.Status);
    }

    [Fact]
    public void Route_UserPromptSubmit_MarksPaneActive()
    {
        var router = new HookEventRouter();
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", PaneId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "stop", PaneId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "user-prompt-submit", PaneId = "p1" });

        var state = router.GetPaneState("p1");
        Assert.Equal("active", state!.Status);
    }

    [Fact]
    public void Route_PreToolUse_RecordsToolUse()
    {
        var router = new HookEventRouter();
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", PaneId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "pre-tool-use", PaneId = "p1" });

        var state = router.GetPaneState("p1");
        Assert.Equal("tool-running", state!.Status);
    }

    [Fact]
    public void Route_PostToolUse_RevertsToActive()
    {
        var router = new HookEventRouter();
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", PaneId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "pre-tool-use", PaneId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "post-tool-use", PaneId = "p1" });

        var state = router.GetPaneState("p1");
        Assert.Equal("active", state!.Status);
    }

    [Fact]
    public void Route_StopFailure_MarksPaneError()
    {
        var router = new HookEventRouter();
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", PaneId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "stop-failure", PaneId = "p1" });

        var state = router.GetPaneState("p1");
        Assert.Equal("error", state!.Status);
    }

    [Fact]
    public void Route_SubagentStart_RecordsSubagent()
    {
        var router = new HookEventRouter();
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", PaneId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "subagent-start", PaneId = "p1" });

        var state = router.GetPaneState("p1");
        Assert.Equal(1, state!.ActiveSubagents);
    }

    [Fact]
    public void Route_SubagentStop_DecrementsSubagent()
    {
        var router = new HookEventRouter();
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", PaneId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "subagent-start", PaneId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "subagent-start", PaneId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "subagent-stop", PaneId = "p1" });

        var state = router.GetPaneState("p1");
        Assert.Equal(1, state!.ActiveSubagents);
    }

    [Fact]
    public void Route_EventWithoutSessionStart_CreatesNoPaneState()
    {
        var router = new HookEventRouter();
        router.Route(new HookEvent { Adapter = "claude-code", Event = "pre-tool-use", PaneId = "p1" });
        Assert.Empty(router.GetAllPaneStates());
    }

    [Fact]
    public void Route_StopWithoutSessionStart_DoesNotSignalNeedsInput()
    {
        var router = new HookEventRouter();
        var fired = false;
        router.NeedsInputTransition += (_, _) => fired = true;
        router.Route(new HookEvent { Adapter = "claude-code", Event = "stop", PaneId = "p1" });
        Assert.False(fired);
        Assert.Empty(router.GetAllPaneStates());
    }

    [Fact]
    public void Route_EventAfterSessionEnd_UpdatesExistingPaneState()
    {
        var router = new HookEventRouter();
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", PaneId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-end", PaneId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "pre-tool-use", PaneId = "p1" });

        var state = router.GetPaneState("p1");
        Assert.Equal("tool-running", state!.Status);
        Assert.Equal("claude-code", state.Adapter);
    }

    [Fact]
    public void Route_WithoutPaneId_NoStateChange()
    {
        var router = new HookEventRouter();
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-start" });
        Assert.Empty(router.GetAllPaneStates());
    }

    [Fact]
    public void DeclaredEvents_AreCanonicalKebab()
    {
        Assert.Contains("session-start", HookEventRouter.DeclaredEvents);
        Assert.Contains("session-end", HookEventRouter.DeclaredEvents);
        Assert.Contains("pre-tool-use", HookEventRouter.DeclaredEvents);
        Assert.Contains("post-tool-use", HookEventRouter.DeclaredEvents);
        Assert.Contains("stop", HookEventRouter.DeclaredEvents);
        Assert.Contains("stop-failure", HookEventRouter.DeclaredEvents);
        Assert.Contains("notification", HookEventRouter.DeclaredEvents);
        Assert.Contains("user-prompt-submit", HookEventRouter.DeclaredEvents);
        Assert.Contains("permission-request", HookEventRouter.DeclaredEvents);
        Assert.Contains("subagent-start", HookEventRouter.DeclaredEvents);
        Assert.Contains("subagent-stop", HookEventRouter.DeclaredEvents);
    }

    [Fact]
    public void GetPaneState_UnknownPane_ReturnsNull()
    {
        var router = new HookEventRouter();
        Assert.Null(router.GetPaneState("nonexistent"));
    }

    [Fact]
    public void Route_Notification_DoesNotChangeStatus()
    {
        var router = new HookEventRouter();
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", PaneId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "notification", PaneId = "p1" });

        var state = router.GetPaneState("p1");
        Assert.Equal("active", state!.Status);
    }
}
