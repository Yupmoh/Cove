using Cove.Tui.Vt;
using Xunit;

namespace Cove.Tui.Tests;

public sealed class VtExtendedColorTests
{
    [Fact]
    public void SGR_TrueColorRed_MapsToBrightRed()
    {
        var vt = new VtEmulator(40, 5);
        vt.Feed("\x1b[38;2;255;0;0mR");
        Assert.Equal('R', vt.Grid.Get(0, 0).Rune);
        Assert.Equal(Cove.Tui.Compositor.CellColor.BrightRed, vt.Grid.Get(0, 0).Fg);
    }

    [Fact]
    public void SGR_TrueColorGreen_MapsToBrightGreen()
    {
        var vt = new VtEmulator(40, 5);
        vt.Feed("\x1b[38;2;0;255;0mG");
        Assert.Equal(Cove.Tui.Compositor.CellColor.BrightGreen, vt.Grid.Get(0, 0).Fg);
    }

    [Fact]
    public void SGR_TrueColorBlue_MapsToBrightBlue()
    {
        var vt = new VtEmulator(40, 5);
        vt.Feed("\x1b[38;2;0;0;255mB");
        Assert.Equal(Cove.Tui.Compositor.CellColor.BrightBlue, vt.Grid.Get(0, 0).Fg);
    }

    [Fact]
    public void SGR_TrueColorDimRed_MapsToRed()
    {
        var vt = new VtEmulator(40, 5);
        vt.Feed("\x1b[38;2;100;0;0mR");
        Assert.Equal(Cove.Tui.Compositor.CellColor.Red, vt.Grid.Get(0, 0).Fg);
    }

    [Fact]
    public void SGR_256ColorIndex1_MapsToRed()
    {
        var vt = new VtEmulator(40, 5);
        vt.Feed("\x1b[38;5;1mR");
        Assert.Equal(Cove.Tui.Compositor.CellColor.Red, vt.Grid.Get(0, 0).Fg);
    }

    [Fact]
    public void SGR_256ColorIndex2_MapsToGreen()
    {
        var vt = new VtEmulator(40, 5);
        vt.Feed("\x1b[38;5;2mG");
        Assert.Equal(Cove.Tui.Compositor.CellColor.Green, vt.Grid.Get(0, 0).Fg);
    }

    [Fact]
    public void SGR_TrueColorBg_MapsToBackground()
    {
        var vt = new VtEmulator(40, 5);
        vt.Feed("\x1b[48;2;255;0;0mX");
        Assert.Equal(Cove.Tui.Compositor.CellColor.BrightRed, vt.Grid.Get(0, 0).Bg);
    }

    [Fact]
    public void SGR_TrueColorDoesNotCorruptAttr()
    {
        var vt = new VtEmulator(40, 5);
        vt.Feed("\x1b[1m\x1b[38;2;255;0;0mR");
        Assert.True(vt.Grid.Get(0, 0).Attr.HasFlag(Cove.Tui.Compositor.CellAttr.Bold));
        Assert.Equal(Cove.Tui.Compositor.CellColor.BrightRed, vt.Grid.Get(0, 0).Fg);
    }

    [Fact]
    public void SGR_TrueColorFollowedByAttr_ParsesCorrectly()
    {
        var vt = new VtEmulator(40, 5);
        vt.Feed("\x1b[38;2;255;0;0m\x1b[4mX");
        Assert.Equal(Cove.Tui.Compositor.CellColor.BrightRed, vt.Grid.Get(0, 0).Fg);
        Assert.True(vt.Grid.Get(0, 0).Attr.HasFlag(Cove.Tui.Compositor.CellAttr.Underline));
    }

    [Fact]
    public void SGR_TrueColorTrailingParamsDoNotLeakAsAttrs()
    {
        var vt = new VtEmulator(40, 5);
        vt.Feed("\x1b[38;2;255;0;0mR");
        Assert.Equal(Cove.Tui.Compositor.CellColor.BrightRed, vt.Grid.Get(0, 0).Fg);
        Assert.Equal(Cove.Tui.Compositor.CellAttr.None, vt.Grid.Get(0, 0).Attr);
    }

    [Fact]
    public void SGR_TrueColorGray_DoesNotRenderAsYellow()
    {
        var vt = new VtEmulator(40, 5);
        vt.Feed("\x1b[38;2;128;128;128mG");
        Assert.Equal(Cove.Tui.Compositor.CellColor.BrightBlack, vt.Grid.Get(0, 0).Fg);
    }

    [Fact]
    public void SGR_TrueColorLightGray_MapsToBrightWhite()
    {
        var vt = new VtEmulator(40, 5);
        vt.Feed("\x1b[38;2;220;220;220mG");
        Assert.Equal(Cove.Tui.Compositor.CellColor.BrightWhite, vt.Grid.Get(0, 0).Fg);
    }

    [Fact]
    public void SGR_TrueColorDarkGray_MapsToBlack()
    {
        var vt = new VtEmulator(40, 5);
        vt.Feed("\x1b[38;2;30;30;30mG");
        Assert.Equal(Cove.Tui.Compositor.CellColor.Black, vt.Grid.Get(0, 0).Fg);
    }
}
