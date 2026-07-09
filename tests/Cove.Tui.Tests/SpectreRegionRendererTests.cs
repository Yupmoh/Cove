using Spectre.Console;
using Cove.Tui.Backend;
using Cove.Tui.Vt;
using Xunit;

namespace Cove.Tui.Tests;

public sealed class SpectreRegionRendererTests
{
    [Fact]
    public void Render_Table_WritesCellsIntoGrid()
    {
        var vt = new VtEmulator(60, 10);
        var renderer = new SpectreRegionRenderer(vt);

        var table = new Table()
            .AddColumn("Name")
            .AddColumn("Value")
            .AddRow("Alpha", "1")
            .AddRow("Beta", "2");

        renderer.Render(table, 60, 10);

        var grid = vt.Grid;
        var allRunes = "";
        for (var y = 0; y < 10; y++)
            for (var x = 0; x < 60; x++)
            {
                var r = grid.Get(x, y).Rune;
                if (r != ' ') allRunes += r;
            }

        Assert.Contains("Name", allRunes);
        Assert.Contains("Value", allRunes);
        Assert.Contains("Alpha", allRunes);
        Assert.Contains("Beta", allRunes);
    }

    [Fact]
    public void Render_Markup_AppliesColor()
    {
        var vt = new VtEmulator(40, 5);
        var renderer = new SpectreRegionRenderer(vt);

        var markup = new Markup("[red]Error[/]");
        renderer.Render(markup, 40, 5);

        var foundRed = false;
        for (var y = 0; y < 5; y++)
            for (var x = 0; x < 40; x++)
            {
                if (grid_Fg(vt, x, y) == Cove.Tui.Compositor.CellColor.Red)
                    foundRed = true;
            }
        Assert.True(foundRed);
    }

    private static Cove.Tui.Compositor.CellColor grid_Fg(VtEmulator vt, int x, int y)
        => vt.Grid.Get(x, y).Fg;
}
