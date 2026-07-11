using Cove.Engine.Lifecycle;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class AgentLifecycleControllerTests
{
    [Fact]
    public void Stop_RecordsStopRequest_KeepsNook()
    {
        var ctrl = new AgentLifecycleController();
        ctrl.Register("p1", "claude-code");

        ctrl.Stop("p1");

        var state = ctrl.GetState("p1");
        Assert.Equal(LifecycleState.Stopped, state!.State);
        Assert.True(state.NookPreserved);
    }

    [Fact]
    public void Close_RecordsCloseRequest_RemovesNook()
    {
        var ctrl = new AgentLifecycleController();
        ctrl.Register("p1", "claude-code");

        ctrl.Close("p1");

        var state = ctrl.GetState("p1");
        Assert.Equal(LifecycleState.Closed, state!.State);
        Assert.False(state.NookPreserved);
    }

    [Fact]
    public void RecordError_CapturesCommandForReplay()
    {
        var ctrl = new AgentLifecycleController();
        ctrl.Register("p1", "claude-code");

        ctrl.RecordError("p1", "claude", exitCode: 1, signal: null);

        var state = ctrl.GetState("p1");
        Assert.Equal(LifecycleState.Errored, state!.State);
        Assert.Equal("claude", state.SurfacedCommand);
        Assert.Equal(1, state.ExitCode);
    }

    [Fact]
    public void RecordError_SignalKill_CapturesSignal()
    {
        var ctrl = new AgentLifecycleController();
        ctrl.Register("p1", "claude-code");

        ctrl.RecordError("p1", "claude", exitCode: null, signal: 9);

        var state = ctrl.GetState("p1");
        Assert.Equal(LifecycleState.Errored, state!.State);
        Assert.Equal(9, state.Signal);
    }

    [Fact]
    public void Replay_ReturnsSurfacedCommand()
    {
        var ctrl = new AgentLifecycleController();
        ctrl.Register("p1", "claude-code");
        ctrl.RecordError("p1", "claude", 1, null);

        var replay = ctrl.GetReplayInfo("p1");
        Assert.NotNull(replay);
        Assert.Equal("claude", replay!.Command);
        Assert.Equal(1, replay.ExitCode);
    }

    [Fact]
    public void Replay_NoError_ReturnsNull()
    {
        var ctrl = new AgentLifecycleController();
        ctrl.Register("p1", "claude-code");

        Assert.Null(ctrl.GetReplayInfo("p1"));
    }

    [Fact]
    public void RecordSpawnedNook_TracksChildNook()
    {
        var ctrl = new AgentLifecycleController();
        ctrl.Register("p1", "claude-code");
        ctrl.Register("p2", "codex");

        ctrl.RecordSpawnedNook("p1", "p2");

        var children = ctrl.GetSpawnedNooks("p1");
        Assert.Single(children);
        Assert.Equal("p2", children[0]);
    }

    [Fact]
    public void RecordSpawnedNook_MultipleChildren_TracksAll()
    {
        var ctrl = new AgentLifecycleController();
        ctrl.Register("p1", "claude-code");
        ctrl.Register("p2", "codex");
        ctrl.Register("p3", "gemini");

        ctrl.RecordSpawnedNook("p1", "p2");
        ctrl.RecordSpawnedNook("p1", "p3");

        var children = ctrl.GetSpawnedNooks("p1");
        Assert.Equal(2, children.Count);
    }

    [Fact]
    public void GetSpawnedNooks_NoChildren_ReturnsEmpty()
    {
        var ctrl = new AgentLifecycleController();
        ctrl.Register("p1", "claude-code");

        var children = ctrl.GetSpawnedNooks("p1");
        Assert.Empty(children);
    }

    [Fact]
    public void Unregister_RemovesState()
    {
        var ctrl = new AgentLifecycleController();
        ctrl.Register("p1", "claude-code");
        ctrl.Unregister("p1");
        Assert.Null(ctrl.GetState("p1"));
    }

    [Fact]
    public void ClearError_ResumesToActive()
    {
        var ctrl = new AgentLifecycleController();
        ctrl.Register("p1", "claude-code");
        ctrl.RecordError("p1", "claude", 1, null);

        ctrl.ClearError("p1");

        var state = ctrl.GetState("p1");
        Assert.Equal(LifecycleState.Active, state!.State);
        Assert.Null(state.SurfacedCommand);
    }
}
