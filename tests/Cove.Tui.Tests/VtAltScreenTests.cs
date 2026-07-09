using Cove.Tui.Vt;
using Xunit;

namespace Cove.Tui.Tests;

public sealed class VtAltScreenTests
{
    private static VtEmulator New(int w = 20, int h = 5) => new(w, h);

    [Fact]
    public void AltScreenEnter_ClearsAndSavesMainBuffer()
    {
        var vt = New();
        vt.Feed("MainContent");
        vt.Feed("\x1b[?1049h");
        Assert.Equal(' ', vt.Grid.Get(0, 0).Rune);
        Assert.Equal(0, vt.CursorX);
        Assert.Equal(0, vt.CursorY);
    }

    [Fact]
    public void AltScreenExit_RestoresMainBuffer()
    {
        var vt = New();
        vt.Feed("MainContent");
        vt.Feed("\x1b[?1049h");
        vt.Feed("AltContent");
        vt.Feed("\x1b[?1049l");
        Assert.Equal('M', vt.Grid.Get(0, 0).Rune);
        Assert.Equal('n', vt.Grid.Get(3, 0).Rune);
    }

    [Fact]
    public void AltScreen_DoesNotWriteToScrollback()
    {
        var vt = New(10, 3);
        vt.Feed("Line1\nLine2\nLine3\nLine4");
        var scrollbackBefore = vt.ScrollbackCount;
        vt.Feed("\x1b[?1049h");
        vt.Feed("\x1b[?1049l");
        Assert.Equal(scrollbackBefore, vt.ScrollbackCount);
    }

    [Fact]
    public void Scrollback_GrowsWhenContentScrollsOff()
    {
        var vt = New(10, 3);
        vt.Feed("A\nB\nC\nD");
        Assert.True(vt.ScrollbackCount >= 1);
    }

    [Fact]
    public void Scrollback_LinesPreservedInOrder()
    {
        var vt = New(10, 3);
        vt.Feed("AA\nBB\nCC\nDD\nEE");
        Assert.Equal('A', vt.GetScrollbackLine(0)[0]);
        Assert.Equal('B', vt.GetScrollbackLine(1)[0]);
    }

    [Fact]
    public void Newline_PastBottom_ScrollsUpWithinScrollRegion()
    {
        var vt = New(10, 4);
        vt.Feed("R0\nR1\nR2\nR3\nR4");
        Assert.Equal('R', vt.Grid.Get(0, 0).Rune);
        Assert.Equal('1', vt.Grid.Get(1, 0).Rune);
    }

    [Fact]
    public void Decstm_SetsScrollRegion()
    {
        var vt = New(10, 6);
        vt.Feed("\x1b[2;5r");
        Assert.Equal(1, vt.ScrollRegionTop);
        Assert.Equal(4, vt.ScrollRegionBottom);
    }

    [Fact]
    public void Decstm_ScrollOnlyWithinRegion()
    {
        var vt = New(10, 6);
        vt.Feed("L0\nL1\nL2\nL3\nL4\nL5");
        vt.Feed("\x1b[2;4r");
        vt.Feed("\x1b[2;1H");
        for (var i = 0; i < 6; i++)
            vt.Feed("X\n");
        Assert.Equal('L', vt.Grid.Get(0, 0).Rune);
        Assert.Equal('L', vt.Grid.Get(0, 5).Rune);
    }

    [Fact]
    public void Decstm_Reset_ClearsScrollRegion()
    {
        var vt = New(10, 6);
        vt.Feed("\x1b[2;5r");
        vt.Feed("\x1b[r");
        Assert.Equal(0, vt.ScrollRegionTop);
        Assert.Equal(vt.Grid.Height - 1, vt.ScrollRegionBottom);
    }

    [Fact]
    public void AltScreen_CursorRestoredOnExit()
    {
        var vt = New();
        vt.Feed("Hi");
        vt.Feed("\x1b[?1049h");
        vt.Feed("\x1b[5;3H");
        vt.Feed("\x1b[?1049l");
        Assert.Equal(2, vt.CursorX);
        Assert.Equal(0, vt.CursorY);
    }

    [Fact]
    public void Scrollback_CappedAtMaxLines()
    {
        var vt = new VtEmulator(10, 2, maxScrollback: 5);
        for (var i = 0; i < 20; i++)
            vt.Feed($"L{i}\n");
        Assert.True(vt.ScrollbackCount <= 5);
    }
}
