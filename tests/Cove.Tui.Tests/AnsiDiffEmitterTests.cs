using Cove.Tui.Compositor;
using Cove.Tui.Emit;
using Xunit;

namespace Cove.Tui.Tests;

public sealed class AnsiDiffEmitterTests
{
    [Fact]
    public void EmptyGrid_NoDirty_ProducesEmptyOutput()
    {
        var grid = new CellGrid(10, 5);
        var emitter = new AnsiDiffEmitter();
        var output = emitter.Emit(grid);
        Assert.Empty(output);
    }

    [Fact]
    public void DirtyCell_EmitsCursorMoveAndChar()
    {
        var grid = new CellGrid(10, 5);
        grid.Set(2, 1, new Cell('X'));
        var emitter = new AnsiDiffEmitter();
        var output = emitter.Emit(grid);
        Assert.Contains("X", output);
        Assert.Contains("\x1b[2;3H", output);
    }

    [Fact]
    public void MultipleDirtyCells_CoalescesContiguousRuns()
    {
        var grid = new CellGrid(10, 5);
        grid.Set(0, 0, new Cell('A'));
        grid.Set(1, 0, new Cell('B'));
        grid.Set(2, 0, new Cell('C'));
        var emitter = new AnsiDiffEmitter();
        var output = emitter.Emit(grid);
        Assert.Contains("ABC", output);
    }

    [Fact]
    public void DirtyCell_WithColor_EmitsSGR()
    {
        var grid = new CellGrid(10, 5);
        grid.Set(0, 0, new Cell('R', CellColor.Red));
        var emitter = new AnsiDiffEmitter();
        var output = emitter.Emit(grid);
        Assert.Contains("\x1b[31m", output);
        Assert.Contains("\x1b[0m", output);
    }

    [Fact]
    public void Emit_ClearsDirtyFlags()
    {
        var grid = new CellGrid(10, 5);
        grid.Set(0, 0, new Cell('X'));
        var emitter = new AnsiDiffEmitter();
        emitter.Emit(grid);
        Assert.Empty(grid.GetDirtyRegions());
    }

    [Fact]
    public void ColorChange_BetweenCells_EmitsNewSGR()
    {
        var grid = new CellGrid(10, 5);
        grid.Set(0, 0, new Cell('R', CellColor.Red));
        grid.Set(1, 0, new Cell('G', CellColor.Green));
        var emitter = new AnsiDiffEmitter();
        var output = emitter.Emit(grid);
        Assert.Contains("\x1b[31mR", output);
        Assert.Contains("\x1b[32mG", output);
    }
}
