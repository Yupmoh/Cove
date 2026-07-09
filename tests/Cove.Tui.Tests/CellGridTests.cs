using Cove.Tui.Compositor;
using Xunit;

namespace Cove.Tui.Tests;

public sealed class CellGridTests
{
    [Fact]
    public void NewGrid_AllCellsAreSpace()
    {
        var grid = new CellGrid(10, 5);
        var cell = grid.Get(0, 0);
        Assert.Equal(' ', cell.Rune);
        Assert.Equal(CellColor.Default, cell.Fg);
        Assert.Equal(CellColor.Default, cell.Bg);
    }

    [Fact]
    public void Set_UpdatesCell()
    {
        var grid = new CellGrid(10, 5);
        grid.Set(2, 1, new Cell('X', CellColor.Red, CellColor.Default));
        var cell = grid.Get(2, 1);
        Assert.Equal('X', cell.Rune);
        Assert.Equal(CellColor.Red, cell.Fg);
    }

    [Fact]
    public void Set_MarksCellDirty()
    {
        var grid = new CellGrid(10, 5);
        Assert.False(grid.IsDirty(2, 1));
        grid.Set(2, 1, new Cell('X'));
        Assert.True(grid.IsDirty(2, 1));
    }

    [Fact]
    public void ClearDirty_ResetsAllDirtyFlags()
    {
        var grid = new CellGrid(10, 5);
        grid.Set(0, 0, new Cell('A'));
        grid.Set(1, 1, new Cell('B'));
        grid.ClearDirty();
        Assert.False(grid.IsDirty(0, 0));
        Assert.False(grid.IsDirty(1, 1));
    }

    [Fact]
    public void Resize_PreservesOverlappingContent()
    {
        var grid = new CellGrid(10, 5);
        grid.Set(0, 0, new Cell('X'));
        grid.Resize(20, 10);
        Assert.Equal('X', grid.Get(0, 0).Rune);
        Assert.Equal(20, grid.Width);
        Assert.Equal(10, grid.Height);
    }

    [Fact]
    public void Fill_FillsAllCellsWithChar()
    {
        var grid = new CellGrid(5, 3);
        grid.Fill(new Cell('.'));
        for (var y = 0; y < 3; y++)
            for (var x = 0; x < 5; x++)
                Assert.Equal('.', grid.Get(x, y).Rune);
    }

    [Fact]
    public void GetDirtyRegions_ReturnsOnlyDirtyCells()
    {
        var grid = new CellGrid(10, 5);
        grid.Set(1, 1, new Cell('A'));
        grid.Set(3, 2, new Cell('B'));
        var dirty = grid.GetDirtyRegions();
        Assert.Equal(2, dirty.Count);
        Assert.Contains(dirty, d => d.X == 1 && d.Y == 1);
        Assert.Contains(dirty, d => d.X == 3 && d.Y == 2);
    }

    [Fact]
    public void Set_OutOfBounds_IsIgnored()
    {
        var grid = new CellGrid(10, 5);
        grid.Set(20, 20, new Cell('X'));
        Assert.Empty(grid.GetDirtyRegions());
    }

    [Fact]
    public void Clear_ResetsAllCellsToSpace()
    {
        var grid = new CellGrid(10, 5);
        grid.Set(0, 0, new Cell('X', CellColor.Red));
        grid.Clear();
        Assert.Equal(' ', grid.Get(0, 0).Rune);
        Assert.Equal(CellColor.Default, grid.Get(0, 0).Fg);
    }

    [Fact]
    public void DoubleBuffer_SwapCommitsToFront()
    {
        var grid = new CellGrid(10, 5);
        grid.Set(0, 0, new Cell('A'));
        grid.SwapBuffers();
        Assert.Equal('A', grid.Get(0, 0).Rune);
        Assert.False(grid.IsDirty(0, 0));
    }
}
