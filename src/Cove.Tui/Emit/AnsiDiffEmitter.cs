using System.Text;
using Cove.Tui.Compositor;

namespace Cove.Tui.Emit;

public sealed class AnsiDiffEmitter
{
    private CellColor _curFg = CellColor.Default;
    private CellColor _curBg = CellColor.Default;
    private CellAttr _curAttr = CellAttr.None;
    private int _curX = -1;
    private int _curY = -1;

    public string Emit(CellGrid grid)
    {
        var dirty = grid.GetDirtyRegions();
        if (dirty.Count == 0)
            return "";

        var sb = new StringBuilder();

        foreach (var cell in dirty)
        {
            if (cell.X != _curX || cell.Y != _curY)
            {
                sb.Append($"\x1b[{cell.Y + 1};{cell.X + 1}H");
            }

            if (cell.Cell.Fg != _curFg || cell.Cell.Bg != _curBg || cell.Cell.Attr != _curAttr)
            {
                EmitSGRReset(sb);
                EmitSGR(sb, cell.Cell.Fg, cell.Cell.Bg, cell.Cell.Attr);
                _curFg = cell.Cell.Fg;
                _curBg = cell.Cell.Bg;
                _curAttr = cell.Cell.Attr;
            }

            sb.Append(cell.Cell.Rune);
            _curX = cell.X + 1;
            _curY = cell.Y;
        }

        if (_curFg != CellColor.Default || _curBg != CellColor.Default || _curAttr != CellAttr.None)
        {
            sb.Append("\x1b[0m");
            _curFg = CellColor.Default;
            _curBg = CellColor.Default;
            _curAttr = CellAttr.None;
        }

        grid.ClearDirty();
        return sb.ToString();
    }

    private static void EmitSGRReset(StringBuilder sb)
    {
        sb.Append("\x1b[0m");
    }

    private static void EmitSGR(StringBuilder sb, CellColor fg, CellColor bg, CellAttr attr)
    {
        var parts = new List<int>();
        if (attr.HasFlag(CellAttr.Bold)) parts.Add(1);
        if (attr.HasFlag(CellAttr.Dim)) parts.Add(2);
        if (attr.HasFlag(CellAttr.Italic)) parts.Add(3);
        if (attr.HasFlag(CellAttr.Underline)) parts.Add(4);
        if (attr.HasFlag(CellAttr.Reverse)) parts.Add(7);
        if (fg != CellColor.Default) parts.Add(ToAnsiFg(fg));
        if (bg != CellColor.Default) parts.Add(ToAnsiBg(bg));

        if (parts.Count > 0)
        {
            sb.Append("\x1b[");
            sb.Append(string.Join(";", parts));
            sb.Append('m');
        }
    }

    private static int ToAnsiFg(CellColor c) => c switch
    {
        CellColor.Black => 30,
        CellColor.Red => 31,
        CellColor.Green => 32,
        CellColor.Yellow => 33,
        CellColor.Blue => 34,
        CellColor.Magenta => 35,
        CellColor.Cyan => 36,
        CellColor.White => 37,
        CellColor.BrightBlack => 90,
        CellColor.BrightRed => 91,
        CellColor.BrightGreen => 92,
        CellColor.BrightYellow => 93,
        CellColor.BrightBlue => 94,
        CellColor.BrightMagenta => 95,
        CellColor.BrightCyan => 96,
        CellColor.BrightWhite => 97,
        _ => 39,
    };

    private static int ToAnsiBg(CellColor c) => ToAnsiFg(c) + 10;
}
