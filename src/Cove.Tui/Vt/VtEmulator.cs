using Cove.Tui.Compositor;

namespace Cove.Tui.Vt;

public sealed class VtEmulator
{
    private readonly CellGrid _grid;
    private int _cursorX;
    private int _cursorY;
    private CellColor _fg = CellColor.Default;
    private CellColor _bg = CellColor.Default;
    private CellAttr _attr = CellAttr.None;
    private readonly int _maxScrollback;
    private readonly System.Collections.Generic.List<Cell[]> _scrollback = new();
    private int _scrollRegionTop;
    private int _scrollRegionBottom;
    private bool _altScreenActive;
    private Cell[]? _savedMainBuffer;
    private int _savedCursorX;
    private int _savedCursorY;

    private enum ParseState { Ground, Esc, Csi, Osc }
    private ParseState _state = ParseState.Ground;
    private readonly char[] _paramBuf = new char[64];
    private int _paramLen;
    private bool _csiPrivate;

    public CellGrid Grid => _grid;
    public int CursorX => _cursorX;
    public int CursorY => _cursorY;
    public int ScrollbackCount => _scrollback.Count;
    public int ScrollRegionTop => _scrollRegionTop;
    public int ScrollRegionBottom => _scrollRegionBottom;

    public VtEmulator(int width, int height, int maxScrollback = 2000)
    {
        _grid = new CellGrid(width, height);
        _maxScrollback = maxScrollback;
        _scrollRegionBottom = height - 1;
    }

    public void Feed(string data)
    {
        Feed(data.AsSpan());
    }

    public void Feed(ReadOnlySpan<char> data)
    {
        foreach (var ch in data)
        {
            switch (_state)
            {
                case ParseState.Ground: ProcessGround(ch); break;
                case ParseState.Esc: ProcessEsc(ch); break;
                case ParseState.Csi: ProcessCsi(ch); break;
                case ParseState.Osc: ProcessOsc(ch); break;
            }
        }
    }

    private void ProcessGround(char ch)
    {
        switch (ch)
        {
            case '\x1b':
                _state = ParseState.Esc;
                break;
            case '\r':
                _cursorX = 0;
                break;
            case '\n':
                _cursorX = 0;
                LineFeed();
                break;
            case '\b':
                if (_cursorX > 0) _cursorX--;
                break;
            case '\t':
                _cursorX = ((_cursorX / 8) + 1) * 8;
                if (_cursorX >= _grid.Width) { _cursorX = 0; LineFeed(); }
                break;
            default:
                if (ch >= 0x20)
                {
                    if (_cursorX >= _grid.Width)
                    {
                        _cursorX = 0;
                        LineFeed();
                    }
                    _grid.Set(_cursorX, _cursorY, new Cell(ch, _fg, _bg, _attr));
                    _cursorX++;
                }
                break;
        }
    }

    private void ProcessEsc(char ch)
    {
        switch (ch)
        {
            case '[':
                _state = ParseState.Csi;
                _paramLen = 0;
                _csiPrivate = false;
                break;
            case ']':
                _state = ParseState.Osc;
                _paramLen = 0;
                break;
            default:
                _state = ParseState.Ground;
                break;
        }
    }

    private void ProcessCsi(char ch)
    {
        if (ch == '?' && _paramLen == 0)
        {
            _csiPrivate = true;
            return;
        }
        if ((ch >= '0' && ch <= '9') || ch == ';' || ch == ':')
        {
            if (_paramLen < _paramBuf.Length)
                _paramBuf[_paramLen++] = ch;
            return;
        }

        var paramStr = new string(_paramBuf, 0, _paramLen);
        var parts = paramStr.Split(';');
        var nums = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            nums[i] = int.TryParse(parts[i], out var n) ? n : 0;

        if (_csiPrivate)
        {
            if (ch == 'h' || ch == 'l')
            {
                var set = ch == 'h';
                foreach (var mode in nums)
                {
                    if (mode == 1049) SetAltScreen(set);
                }
            }
            _state = ParseState.Ground;
            return;
        }
        switch (ch)
        {
            case 'm':
                ProcessSGR(nums);
                break;
            case 'H':
            case 'f':
                var row = nums.Length > 0 ? nums[0] : 1;
                var col = nums.Length > 1 ? nums[1] : 1;
                _cursorY = Math.Max(0, row - 1);
                _cursorX = Math.Max(0, col - 1);
                break;
            case 'A':
                _cursorY -= Math.Max(1, nums[0]);
                ClampCursor();
                break;
            case 'B':
                _cursorY += Math.Max(1, nums[0]);
                ClampCursor();
                break;
            case 'C':
                _cursorX += Math.Max(1, nums[0]);
                ClampCursor();
                break;
            case 'D':
                _cursorX -= Math.Max(1, nums[0]);
                ClampCursor();
                break;
            case 'J':
                if (nums[0] == 2 || nums.Length == 0)
                {
                    _grid.Clear();
                    _cursorX = 0;
                    _cursorY = 0;
                }
                break;
            case 'K':
                break;
            case 'r':
                SetScrollRegion(nums.Length > 0 ? nums[0] : 0, nums.Length > 1 ? nums[1] : 0);
                break;
        }
        _state = ParseState.Ground;
    }

    private void ProcessOsc(char ch)
    {
        if (ch == '\x07' || (ch == '\\' && _paramLen > 0 && _paramBuf[_paramLen - 1] == '\x1b'))
        {
            _state = ParseState.Ground;
        }
        else if (_paramLen < _paramBuf.Length)
        {
            _paramBuf[_paramLen++] = ch;
        }
    }

    private void ProcessSGR(int[] nums)
    {
        if (nums.Length == 0)
        {
            _fg = CellColor.Default;
            _bg = CellColor.Default;
            _attr = CellAttr.None;
            return;
        }

        for (var i = 0; i < nums.Length; i++)
        {
            var n = nums[i];
            switch (n)
            {
                case 0:
                    _fg = CellColor.Default;
                    _bg = CellColor.Default;
                    _attr = CellAttr.None;
                    break;
                case 1:
                    _attr |= CellAttr.Bold;
                    break;
                case 2:
                    _attr |= CellAttr.Dim;
                    break;
                case 3:
                    _attr |= CellAttr.Italic;
                    break;
                case 4:
                    _attr |= CellAttr.Underline;
                    break;
                case 7:
                    _attr |= CellAttr.Reverse;
                    break;
                case 22:
                    _attr &= ~(CellAttr.Bold | CellAttr.Dim);
                    break;
                case 23:
                    _attr &= ~CellAttr.Italic;
                    break;
                case 24:
                    _attr &= ~CellAttr.Underline;
                    break;
                case 27:
                    _attr &= ~CellAttr.Reverse;
                    break;
                case 38:
                    if (i + 1 < nums.Length)
                    {
                        if (nums[i + 1] == 5 && i + 2 < nums.Length)
                        {
                            _fg = Map256Color(nums[i + 2]);
                            i += 2;
                        }
                        else if (nums[i + 1] == 2 && i + 4 < nums.Length)
                        {
                            _fg = MapTrueColor(nums[i + 2], nums[i + 3], nums[i + 4]);
                            i += 4;
                        }
                    }
                    break;
                case >= 30 and <= 37:
                    _fg = (CellColor)(n - 29);
                    break;
                case 39:
                    _fg = CellColor.Default;
                    break;
                case >= 40 and <= 47:
                    _bg = (CellColor)(n - 39);
                    break;
                case 48:
                    if (i + 1 < nums.Length)
                    {
                        if (nums[i + 1] == 5 && i + 2 < nums.Length)
                        {
                            _bg = Map256Color(nums[i + 2]);
                            i += 2;
                        }
                        else if (nums[i + 1] == 2 && i + 4 < nums.Length)
                        {
                            _bg = MapTrueColor(nums[i + 2], nums[i + 3], nums[i + 4]);
                            i += 4;
                        }
                    }
                    break;
                case 49:
                    _bg = CellColor.Default;
                    break;
                case >= 90 and <= 97:
                    _fg = (CellColor)(n - 81);
                    break;
                case >= 100 and <= 107:
                    _bg = (CellColor)(n - 91);
                    break;
            }
        }
    }

    private static CellColor Map256Color(int index)
    {
        if (index >= 0 && index <= 7) return (CellColor)(index + 1);
        if (index >= 8 && index <= 15) return (CellColor)(index + 1);
        if (index >= 16 && index <= 231)
        {
            var i = index - 16;
            var r = i / 36;
            var g = (i / 6) % 6;
            var b = i % 6;
            return MapTrueColor(r == 0 ? 0 : 55 + r * 40, g == 0 ? 0 : 55 + g * 40, b == 0 ? 0 : 55 + b * 40);
        }
        if (index >= 232 && index <= 255)
        {
            var v = 8 + (index - 232) * 10;
            return MapTrueColor(v, v, v);
        }
        return CellColor.Default;
    }

    private static CellColor MapTrueColor(int r, int g, int b)
    {
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var bright = max > 127;
        var nearGray = max - min <= 32;
        if (nearGray)
        {
            if (min < 50) return CellColor.Black;
            if (max > 200) return CellColor.BrightWhite;
            return bright ? CellColor.BrightBlack : CellColor.White;
        }
        if (r >= g && r >= b) return bright ? CellColor.BrightRed : CellColor.Red;
        if (g >= r && g >= b) return bright ? CellColor.BrightGreen : CellColor.Green;
        if (b >= r && b >= g) return bright ? CellColor.BrightBlue : CellColor.Blue;
        return bright ? CellColor.BrightWhite : CellColor.White;
    }

    private void ClampCursor()
    {
        if (_cursorY < 0) _cursorY = 0;
        if (_cursorY >= _grid.Height) _cursorY = _grid.Height - 1;
        if (_cursorX < 0) _cursorX = 0;
        if (_cursorX >= _grid.Width) _cursorX = _grid.Width - 1;
    }

    private void LineFeed()
    {
        if (_cursorY >= _scrollRegionBottom)
        {
            ScrollRegion(1);
            _cursorY = _scrollRegionBottom;
        }
        else
        {
            _cursorY++;
            ClampCursor();
        }
    }

    private void ScrollRegion(int count)
    {
        if (!_altScreenActive && _scrollRegionTop == 0 && _scrollRegionBottom == _grid.Height - 1)
        {
            for (var i = 0; i < count; i++)
                SaveScrollbackLine();
        }
        _grid.ScrollUp(_scrollRegionTop, _scrollRegionBottom, count);
    }

    private void SaveScrollbackLine()
    {
        var line = new Cell[_grid.Width];
        for (var x = 0; x < _grid.Width; x++)
            line[x] = _grid.Get(x, 0);
        _scrollback.Add(line);
        while (_scrollback.Count > _maxScrollback)
            _scrollback.RemoveAt(0);
    }

    private void SetAltScreen(bool enable)
    {
        if (enable && !_altScreenActive)
        {
            _savedMainBuffer = CaptureGrid();
            _savedCursorX = _cursorX;
            _savedCursorY = _cursorY;
            _grid.Clear();
            _cursorX = 0;
            _cursorY = 0;
            _altScreenActive = true;
        }
        else if (!enable && _altScreenActive)
        {
            if (_savedMainBuffer is not null)
                RestoreGrid(_savedMainBuffer);
            _cursorX = _savedCursorX;
            _cursorY = _savedCursorY;
            _altScreenActive = false;
            _savedMainBuffer = null;
        }
    }

    private void SetScrollRegion(int top, int bottom)
    {
        if (top == 0 && bottom == 0)
        {
            _scrollRegionTop = 0;
            _scrollRegionBottom = _grid.Height - 1;
        }
        else
        {
            _scrollRegionTop = Math.Max(0, top - 1);
            _scrollRegionBottom = Math.Min(_grid.Height - 1, bottom - 1);
            if (_scrollRegionTop >= _scrollRegionBottom)
            {
                _scrollRegionTop = 0;
                _scrollRegionBottom = _grid.Height - 1;
            }
        }
        _cursorX = 0;
        _cursorY = _scrollRegionTop;
    }

    private Cell[] CaptureGrid()
    {
        var snap = new Cell[_grid.Width * _grid.Height];
        for (var y = 0; y < _grid.Height; y++)
            for (var x = 0; x < _grid.Width; x++)
                snap[y * _grid.Width + x] = _grid.Get(x, y);
        return snap;
    }

    private void RestoreGrid(Cell[] snap)
    {
        for (var y = 0; y < _grid.Height; y++)
            for (var x = 0; x < _grid.Width; x++)
            {
                var idx = y * _grid.Width + x;
                _grid.Set(x, y, idx < snap.Length ? snap[idx] : Cell.Blank);
            }
    }

    public string GetScrollbackLine(int index)
    {
        if (index < 0 || index >= _scrollback.Count) return "";
        var line = _scrollback[index];
        var sb = new System.Text.StringBuilder(line.Length);
        foreach (var c in line)
            sb.Append(c.Rune);
        return sb.ToString().TrimEnd();
    }

    public void Resize(int width, int height)
    {
        _grid.Resize(width, height);
        _scrollRegionTop = 0;
        _scrollRegionBottom = _grid.Height - 1;
        ClampCursor();
    }
}
