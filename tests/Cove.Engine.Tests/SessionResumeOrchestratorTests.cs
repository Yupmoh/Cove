using Cove.Engine.Restart;
using Cove.Engine.Sessions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class SessionResumeOrchestratorTests
{
    [Fact]
    public void Dismiss_KillsPty_PreservesSession_MarksResumable()
    {
        var orch = new SessionResumeOrchestrator();
        orch.Register("p1", "claude-code", "session-abc");

        orch.Dismiss("p1");

        var state = orch.GetState("p1");
        Assert.Equal(SessionLifecycle.Dismissed, state!.Lifecycle);
        Assert.Equal("session-abc", state.SessionId);
        Assert.True(state.Resumable);
    }

    [Fact]
    public void Dismiss_UnknownNook_LogsAndReturns()
    {
        var orch = new SessionResumeOrchestrator();
        orch.Dismiss("nonexistent");
        Assert.Null(orch.GetState("nonexistent"));
    }

    [Fact]
    public void Background_DetachesFromShore_KeepsPtyAlive()
    {
        var orch = new SessionResumeOrchestrator();
        orch.Register("p1", "claude-code", "session-abc");

        orch.Background("p1");

        var state = orch.GetState("p1");
        Assert.Equal(SessionLifecycle.Background, state!.Lifecycle);
        Assert.True(state.Resumable);
    }

    [Fact]
    public void Foreground_AttachesToShore()
    {
        var orch = new SessionResumeOrchestrator();
        orch.Register("p1", "claude-code", "session-abc");
        orch.Background("p1");

        orch.Foreground("p1");

        var state = orch.GetState("p1");
        Assert.Equal(SessionLifecycle.Active, state!.Lifecycle);
    }

    [Fact]
    public void Stop_HardKills_MarksCancelled_NotResumable()
    {
        var orch = new SessionResumeOrchestrator();
        orch.Register("p1", "claude-code", "session-abc");

        orch.Stop("p1");

        var state = orch.GetState("p1");
        Assert.Equal(SessionLifecycle.Cancelled, state!.Lifecycle);
        Assert.False(state.Resumable);
    }

    [Fact]
    public void Wake_DismissedSession_BuildsResumeCommand()
    {
        var orch = new SessionResumeOrchestrator();
        orch.Register("p1", "claude-code", "session-abc");
        orch.Dismiss("p1");

        var canWake = orch.CanWake("p1");
        Assert.True(canWake);

        orch.MarkWaking("p1");
        var state = orch.GetState("p1");
        Assert.Equal(SessionLifecycle.Waking, state!.Lifecycle);
    }

    [Fact]
    public void Wake_NotDismissed_ReturnsFalse()
    {
        var orch = new SessionResumeOrchestrator();
        orch.Register("p1", "claude-code", "session-abc");

        Assert.False(orch.CanWake("p1"));
    }

    [Fact]
    public void Wake_Cancelled_ReturnsFalse()
    {
        var orch = new SessionResumeOrchestrator();
        orch.Register("p1", "claude-code", "session-abc");
        orch.Stop("p1");

        Assert.False(orch.CanWake("p1"));
    }

    [Fact]
    public void MarkActive_TransitionsFromWaking()
    {
        var orch = new SessionResumeOrchestrator();
        orch.Register("p1", "claude-code", "session-abc");
        orch.Dismiss("p1");
        orch.MarkWaking("p1");
        orch.MarkActive("p1");

        var state = orch.GetState("p1");
        Assert.Equal(SessionLifecycle.Active, state!.Lifecycle);
    }

    [Fact]
    public void Unregister_RemovesSession()
    {
        var orch = new SessionResumeOrchestrator();
        orch.Register("p1", "claude-code", "session-abc");
        orch.Unregister("p1");
        Assert.Null(orch.GetState("p1"));
    }

    [Fact]
    public void ListDismissed_ReturnsOnlyResumableDismissed()
    {
        var orch = new SessionResumeOrchestrator();
        orch.Register("p1", "claude-code", "session-1");
        orch.Register("p2", "codex", "session-2");
        orch.Register("p3", "gemini", "session-3");
        orch.Dismiss("p1");
        orch.Dismiss("p2");
        orch.Stop("p3");

        var dismissed = orch.ListDismissed();
        Assert.Equal(2, dismissed.Count());
    }

    [Fact]
    public void ListBackground_ReturnsOnlyBackgroundSessions()
    {
        var orch = new SessionResumeOrchestrator();
        orch.Register("p1", "claude-code", "session-1");
        orch.Register("p2", "codex", "session-2");
        orch.Background("p1");

        var background = orch.ListBackground();
        Assert.Single(background);
        Assert.Equal("p1", background.First().NookId);
    }
}
