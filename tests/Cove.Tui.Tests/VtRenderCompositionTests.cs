using Cove.Tui.Emit;
using Cove.Tui.Vt;
using Xunit;

namespace Cove.Tui.Tests;

public sealed class VtRenderCompositionTests
{
    [Fact]
    public void FeedThenEmit_ProducesNonEmptyOutputContainingFedChar()
    {
        var vt = new VtEmulator(80, 24);
        var emitter = new AnsiDiffEmitter();

        vt.Feed("X");

        var output = emitter.Emit(vt.Grid);
        Assert.False(string.IsNullOrEmpty(output));
        Assert.Contains("X", output);
    }

    [Fact]
    public void FeedThenEmit_EmptyFeed_ProducesEmptyOutput()
    {
        var vt = new VtEmulator(80, 24);
        var emitter = new AnsiDiffEmitter();

        var output = emitter.Emit(vt.Grid);
        Assert.True(string.IsNullOrEmpty(output));
    }

    [Fact]
    public void FeedThenEmit_MultipleChars_AllRendered()
    {
        var vt = new VtEmulator(80, 24);
        var emitter = new AnsiDiffEmitter();

        vt.Feed("AB");

        var output = emitter.Emit(vt.Grid);
        Assert.Contains("A", output);
        Assert.Contains("B", output);
    }

    [Fact]
    public void FeedThenEmit_StandaloneGridNotUsed_GridPropertyIsSameInstance()
    {
        var vt = new VtEmulator(80, 24);
        var emitter = new AnsiDiffEmitter();

        vt.Feed("Z");

        var gridProp = vt.Grid;
        var output = emitter.Emit(gridProp);
        Assert.Contains("Z", output);
    }
}
