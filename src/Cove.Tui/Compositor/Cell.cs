namespace Cove.Tui.Compositor;

public readonly record struct Cell(char Rune, CellColor Fg = CellColor.Default, CellColor Bg = CellColor.Default, CellAttr Attr = CellAttr.None)
{
    public static Cell Blank => new(' ');
}

[Flags]
public enum CellAttr : byte
{
    None = 0,
    Bold = 1,
    Italic = 2,
    Underline = 4,
    Reverse = 8,
    Dim = 16,
}

public enum CellColor : byte
{
    Default = 0,
    Black = 1,
    Red = 2,
    Green = 3,
    Yellow = 4,
    Blue = 5,
    Magenta = 6,
    Cyan = 7,
    White = 8,
    BrightBlack = 9,
    BrightRed = 10,
    BrightGreen = 11,
    BrightYellow = 12,
    BrightBlue = 13,
    BrightMagenta = 14,
    BrightCyan = 15,
    BrightWhite = 16,
}

public readonly record struct DirtyCell(int X, int Y, Cell Cell);
