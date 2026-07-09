using Cove.Tui.Compositor;
using Cove.Tui.Vt;
using Xunit;

namespace Cove.Tui.Tests;

public sealed class VtEmulatorTests
{
    private static VtEmulator New(int w = 40, int h = 10) => new(w, h);

    [Fact]
    public void PlainText_WritesToGrid()
    {
        var vt = New();
        vt.Feed("Hello");
        Assert.Equal('H', vt.Grid.Get(0, 0).Rune);
        Assert.Equal('e', vt.Grid.Get(1, 0).Rune);
        Assert.Equal('o', vt.Grid.Get(4, 0).Rune);
    }

    [Fact]
    public void Newline_MovesToNextRow()
    {
        var vt = New();
        vt.Feed("AB\nCD");
        Assert.Equal('A', vt.Grid.Get(0, 0).Rune);
        Assert.Equal('C', vt.Grid.Get(0, 1).Rune);
        Assert.Equal('D', vt.Grid.Get(1, 1).Rune);
    }

    [Fact]
    public void CarriageReturn_MovesToColumnZero()
    {
        var vt = New();
        vt.Feed("ABC\rX");
        Assert.Equal('X', vt.Grid.Get(0, 0).Rune);
        Assert.Equal('B', vt.Grid.Get(1, 0).Rune);
    }

    [Fact]
    public void SGR_Red_SetsForegroundColor()
    {
        var vt = New();
        vt.Feed("\x1b[31mR");
        Assert.Equal('R', vt.Grid.Get(0, 0).Rune);
        Assert.Equal(CellColor.Red, vt.Grid.Get(0, 0).Fg);
    }

    [Fact]
    public void SGR_Reset_RestoresDefaultColor()
    {
        var vt = New();
        vt.Feed("\x1b[31mR\x1b[0mG");
        Assert.Equal(CellColor.Red, vt.Grid.Get(0, 0).Fg);
        Assert.Equal(CellColor.Default, vt.Grid.Get(1, 0).Fg);
    }

    [Fact]
    public void SGR_Bold_SetsBoldAttr()
    {
        var vt = New();
        vt.Feed("\x1b[1mB");
        Assert.Equal('B', vt.Grid.Get(0, 0).Rune);
        Assert.True(vt.Grid.Get(0, 0).Attr.HasFlag(CellAttr.Bold));
    }

    [Fact]
    public void CSI_CUP_MovesCursor()
    {
        var vt = New();
        vt.Feed("\x1b[3;5HX");
        Assert.Equal('X', vt.Grid.Get(4, 2).Rune);
    }

    [Fact]
    public void CSI_CUF_MovesCursorRight()
    {
        var vt = New();
        vt.Feed("\x1b[5C X");
        Assert.Equal('X', vt.Grid.Get(6, 0).Rune);
    }

    [Fact]
    public void CSI_CUB_MovesCursorLeft()
    {
        var vt = New();
        vt.Feed("ABCDEF\x1b[3D X");
        Assert.Equal('X', vt.Grid.Get(4, 0).Rune);
    }

    [Fact]
    public void Backspace_MovesCursorLeft()
    {
        var vt = New();
        vt.Feed("AB\bX");
        Assert.Equal('X', vt.Grid.Get(1, 0).Rune);
    }

    [Fact]
    public void Tab_MovesToNextTabStop()
    {
        var vt = New();
        vt.Feed("\tX");
        Assert.Equal('X', vt.Grid.Get(8, 0).Rune);
    }

    [Fact]
    public void ClearScreen_ClearsGrid()
    {
        var vt = New();
        vt.Feed("Hello World");
        vt.Feed("\x1b[2J");
        Assert.Equal(' ', vt.Grid.Get(0, 0).Rune);
    }

    [Fact]
    public void OSC_Title_IsConsumed()
    {
        var vt = New();
        vt.Feed("\x1b]0;My Title\x07Hello");
        Assert.Equal('H', vt.Grid.Get(0, 0).Rune);
    }

    [Fact]
    public void FeedMultipleChunks_MaintainsState()
    {
        var vt = New();
        vt.Feed("AB");
        vt.Feed("CD");
        Assert.Equal('D', vt.Grid.Get(3, 0).Rune);
    }

    [Fact]
    public void WrapAtRightEdge_MovesToNextRow()
    {
        var vt = New(5, 3);
        vt.Feed("ABCDEF");
        Assert.Equal('F', vt.Grid.Get(0, 1).Rune);
    }
}
