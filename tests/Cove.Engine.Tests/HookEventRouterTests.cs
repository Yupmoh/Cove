using System.Collections.Concurrent;
using Cove.Adapters;
using Cove.Engine.Hooks;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class HookEventRouterTests
{
    [Fact]
    public void Route_SessionStart_CreatesIdleNookState()
    {
        var router = new HookEventRouter();
        var ev = new HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" };
        router.Route(ev);

        var state = router.GetNookState("p1");
        Assert.NotNull(state);
        Assert.Equal("claude-code", state!.Adapter);
        Assert.Equal("idle", state.Status);
    }

    [Fact]
    public void Route_SessionEnd_MarksNookIdle()
    {
        var router = new HookEventRouter();
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-end", NookId = "p1" });

        var state = router.GetNookState("p1");
        Assert.NotNull(state);
        Assert.Equal("idle", state!.Status);
    }

    [Fact]
    public void Route_Stop_MarksNookDoneWithoutNeedsInputSignal()
    {
        var router = new HookEventRouter();
        bool? needsInput = null;
        router.NeedsInputTransition += (_, value) => needsInput = value;
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "stop", NookId = "p1" });

        var state = router.GetNookState("p1");
        Assert.NotNull(state);
        Assert.Equal("done", state!.Status);
        Assert.False(needsInput);
    }

    [Fact]
    public void Route_PermissionRequest_MarksNeedsPermissionAndSignals()
    {
        var router = new HookEventRouter();
        bool? needsInput = null;
        router.NeedsInputTransition += (_, value) => needsInput = value;
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "permission-request", NookId = "p1" });

        var state = router.GetNookState("p1");
        Assert.NotNull(state);
        Assert.Equal("needs-permission", state!.Status);
        Assert.True(needsInput);
    }

    [Fact]
    public void Route_UserPromptSubmit_MarksNookActive()
    {
        var router = new HookEventRouter();
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "stop", NookId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "user-prompt-submit", NookId = "p1" });

        var state = router.GetNookState("p1");
        Assert.Equal("active", state!.Status);
    }

    [Fact]
    public void Route_PreToolUse_RecordsToolUse()
    {
        var router = new HookEventRouter();
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "pre-tool-use", NookId = "p1" });

        var state = router.GetNookState("p1");
        Assert.Equal("tool-running", state!.Status);
    }

    [Fact]
    public void Route_PostToolUse_RevertsToActive()
    {
        var router = new HookEventRouter();
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "pre-tool-use", NookId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "post-tool-use", NookId = "p1" });

        var state = router.GetNookState("p1");
        Assert.Equal("active", state!.Status);
    }

    [Fact]
    public void Route_StopFailure_MarksNookError()
    {
        var router = new HookEventRouter();
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "stop-failure", NookId = "p1" });

        var state = router.GetNookState("p1");
        Assert.Equal("error", state!.Status);
    }

    [Fact]
    public void Route_SubagentStart_RecordsSubagent()
    {
        var router = new HookEventRouter();
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "subagent-start", NookId = "p1" });

        var state = router.GetNookState("p1");
        Assert.Equal(1, state!.ActiveSubagents);
    }

    [Fact]
    public void Route_SubagentStop_DecrementsSubagent()
    {
        var router = new HookEventRouter();
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "subagent-start", NookId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "subagent-start", NookId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "subagent-stop", NookId = "p1" });

        var state = router.GetNookState("p1");
        Assert.Equal(1, state!.ActiveSubagents);
    }

    [Fact]
    public void Route_EventWithoutSessionStart_CreatesNoNookState()
    {
        var router = new HookEventRouter();
        router.Route(new HookEvent { Adapter = "claude-code", Event = "pre-tool-use", NookId = "p1" });
        Assert.Empty(router.GetAllNookStates());
    }

    [Fact]
    public void Route_StopWithoutSessionStart_DoesNotSignalNeedsInput()
    {
        var router = new HookEventRouter();
        var fired = false;
        router.NeedsInputTransition += (_, _) => fired = true;
        router.Route(new HookEvent { Adapter = "claude-code", Event = "stop", NookId = "p1" });
        Assert.False(fired);
        Assert.Empty(router.GetAllNookStates());
    }

    [Fact]
    public void Route_EventAfterSessionEnd_UpdatesExistingNookState()
    {
        var router = new HookEventRouter();
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-end", NookId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "pre-tool-use", NookId = "p1" });

        var state = router.GetNookState("p1");
        Assert.Equal("tool-running", state!.Status);
        Assert.Equal("claude-code", state.Adapter);
    }

    [Fact]
    public void Route_WithoutNookId_NoStateChange()
    {
        var router = new HookEventRouter();
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-start" });
        Assert.Empty(router.GetAllNookStates());
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
    public void GetNookState_UnknownNook_ReturnsNull()
    {
        var router = new HookEventRouter();
        Assert.Null(router.GetNookState("nonexistent"));
    }

    [Fact]
    public void Seed_TracksNookAsIdle_SoLaterEventsAreNotDropped()
    {
        var router = new HookEventRouter();
        router.Seed("p-restored", "claude-code", "sess-1");

        var seeded = router.GetNookState("p-restored");
        Assert.NotNull(seeded);
        Assert.Equal("idle", seeded!.Status);
        Assert.Equal("sess-1", seeded.SessionId);

        router.Route(new HookEvent { Adapter = "claude-code", Event = "pre-tool-use", NookId = "p-restored" });
        Assert.Equal("tool-running", router.GetNookState("p-restored")!.Status);
    }

    [Fact]
    public void Seed_DoesNotOverwriteExistingLiveState()
    {
        var router = new HookEventRouter();
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "pre-tool-use", NookId = "p1" });

        router.Seed("p1", "claude-code", "sess-x");

        Assert.Equal("tool-running", router.GetNookState("p1")!.Status);
    }

    [Fact]
    public void SessionEndForSeededRestoredNook_TransitionsInsteadOfDropping()
    {
        var router = new HookEventRouter();
        router.Seed("p-restored", "claude-code");

        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-end", NookId = "p-restored" });

        var state = router.GetNookState("p-restored");
        Assert.NotNull(state);
        Assert.Equal("idle", state!.Status);
    }

    [Fact]
    public void Route_Notification_MarksNeedsInputAndSignals()
    {
        var router = new HookEventRouter();
        bool? needsInput = null;
        router.NeedsInputTransition += (_, value) => needsInput = value;
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "notification", NookId = "p1" });

        var state = router.GetNookState("p1");
        Assert.NotNull(state);
        Assert.Equal("needs-input", state!.Status);
        Assert.True(needsInput);
    }

    [Fact]
    public void Acknowledge_DoneNook_TransitionsToIdle()
    {
        var router = new HookEventRouter();
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "stop", NookId = "p1" });

        Assert.True(router.Acknowledge("p1"));
        Assert.Equal("idle", router.GetNookState("p1")!.Status);
    }

    [Fact]
    public void Acknowledge_CrashedNook_TransitionsToIdleAndClearsStopReason()
    {
        var router = new HookEventRouter();
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "stop-failure", NookId = "p1" });

        Assert.True(router.Acknowledge("p1"));
        var state = router.GetNookState("p1")!;
        Assert.Equal("idle", state.Status);
        Assert.Null(state.StopReason);
    }

    [Fact]
    public void Acknowledge_ActiveNook_IsRefused()
    {
        var router = new HookEventRouter();
        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "user-prompt-submit", NookId = "p1" });

        Assert.False(router.Acknowledge("p1"));
        Assert.Equal("active", router.GetNookState("p1")!.Status);
    }

    [Fact]
    public void Acknowledge_UnknownNook_IsRefused()
    {
        var router = new HookEventRouter();
        Assert.False(router.Acknowledge("missing"));
    }
}
