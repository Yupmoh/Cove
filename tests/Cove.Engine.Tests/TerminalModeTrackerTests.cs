using System.Text;
using Cove.Engine.Pty;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class TerminalModeTrackerTests
{
    [Fact]
    public void Feed_RestoresCurrentPrivateModesInStableOrder()
    {
        var tracker = new TerminalModeTracker();

        tracker.Feed(Encoding.ASCII.GetBytes("\x1b[?1049h\x1b[?1003;1006h\x1b[?1h\x1b[?1l"));

        Assert.Equal("\x1b[?1l\x1b[?1003h\x1b[?1006h\x1b[?1049h", tracker.BuildPreamble());
    }

    [Fact]
    public void Feed_TracksSequencesSplitAcrossReads()
    {
        var tracker = new TerminalModeTracker();

        tracker.Feed(Encoding.ASCII.GetBytes("before\x1b[?10"));
        Assert.Empty(tracker.BuildPreamble());
        tracker.Feed(Encoding.ASCII.GetBytes("49hafter\x1b[?1049"));
        Assert.Equal("\x1b[?1049h", tracker.BuildPreamble());
        tracker.Feed(Encoding.ASCII.GetBytes("l"));

        Assert.Equal("\x1b[?1049l", tracker.BuildPreamble());
    }

    [Fact]
    public void BuildCheckpointSupplement_ExcludesAlternateScreenTransitions()
    {
        var tracker = new TerminalModeTracker();
        tracker.Feed(Encoding.ASCII.GetBytes("\x1b[?47h\x1b[?1003;1006;1007h\x1b[?1047h\x1b[?1048h\x1b[?1049h\x1b[?2004h"));

        Assert.Equal("\x1b[?1003h\x1b[?1006h\x1b[?1007h\x1b[?2004h", tracker.BuildCheckpointSupplement());
    }

    [Fact]
    public void Feed_IgnoresUntrackedAndNonPrivateModes()
    {
        var tracker = new TerminalModeTracker();

        tracker.Feed(Encoding.ASCII.GetBytes("\x1b[31m\x1b[?25l\x1b[999h"));

        Assert.Empty(tracker.BuildPreamble());
    }
}
