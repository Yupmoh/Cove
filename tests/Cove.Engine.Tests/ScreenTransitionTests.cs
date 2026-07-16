using Cove.Engine.Hooks;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ScreenTransitionTests
{
    [Fact]
    public void ScreenTransition_UpdatesStateAndFiresNeedsInput()
    {
        var router = new HookEventRouter();
        var signals = new List<(string NookId, bool NeedsInput)>();
        router.NeedsInputTransition += (nookId, needsInput) => signals.Add((nookId, needsInput));

        router.ScreenTransition("nook-1", "opencode", "needs-permission");
        Assert.Equal("needs-permission", router.GetNookState("nook-1")!.Status);
        Assert.Equal(("nook-1", true), signals.Single());

        router.ScreenTransition("nook-1", "opencode", "active");
        Assert.Equal("active", router.GetNookState("nook-1")!.Status);
        Assert.Equal(("nook-1", false), signals[1]);
    }

    [Fact]
    public void ScreenTransition_SameStatus_NoSignalNoTimestampChurn()
    {
        var router = new HookEventRouter();
        var signals = 0;
        router.NeedsInputTransition += (_, _) => signals++;

        router.ScreenTransition("nook-2", "pi", "active");
        var first = router.GetNookState("nook-2")!;
        router.ScreenTransition("nook-2", "pi", "active");
        var second = router.GetNookState("nook-2")!;

        Assert.Equal(first.LastEventAt, second.LastEventAt);
        Assert.Equal(0, signals);
    }

    [Fact]
    public void ScreenTransition_NonInputStatuses_DoNotSignal()
    {
        var router = new HookEventRouter();
        var signals = 0;
        router.NeedsInputTransition += (_, _) => signals++;

        router.ScreenTransition("nook-3", "hermes", "active");
        router.ScreenTransition("nook-3", "hermes", "idle");
        Assert.Equal(0, signals);
    }

    [Fact]
    public void ScreenTransition_CreatesStateForUnseededNook()
    {
        var router = new HookEventRouter();
        router.ScreenTransition("nook-4", "openclaw", "needs-input");
        var state = router.GetNookState("nook-4")!;
        Assert.Equal("openclaw", state.Adapter);
        Assert.Equal("needs-input", state.Status);
    }
}
