namespace Cove.Tui.Compositor;

public sealed class CellGrid
{
    private Cell[] _front;
    private Cell[] _back;
    private bool[] _dirty;
    private int _width;
    private int _height;

    public int Width => _width;
    public int Height => _height;

    public CellGrid(int width, int height)
    {
        _width = Math.Max(1, width);
        _height = Math.Max(1, height);
        var size = _width * _height;
        _front = new Cell[size];
        _back = new Cell[size];
        _dirty = new bool[size];
        Array.Fill(_front, Cell.Blank);
        Array.Fill(_back, Cell.Blank);
    }

    public Cell Get(int x, int y)
    {
        if (x < 0 || x >= _width || y < 0 || y >= _height)
            return Cell.Blank;
        return _back[y * _width + x];
    }
    public void Set(int x, int y, Cell cell)
    {
        if (x < 0 || x >= _width || y < 0 || y >= _height)
            return;
        var idx = y * _width + x;
        _back[idx] = cell;
        _dirty[idx] = true;
    }

    public bool IsDirty(int x, int y)
    {
        if (x < 0 || x >= _width || y < 0 || y >= _height)
            return false;
        return _dirty[y * _width + x];
    }

    public void ClearDirty()
    {
        Array.Clear(_dirty, 0, _dirty.Length);
    }

    public void Clear()
    {
        Array.Fill(_back, Cell.Blank);
        for (var i = 0; i < _dirty.Length; i++)
            _dirty[i] = true;
    }

    public void Fill(Cell cell)
    {
        Array.Fill(_back, cell);
        Array.Fill(_dirty, true);
    }

    public void SwapBuffers()
    {
        (_front, _back) = (_back, _front);
        Array.Fill(_back, Cell.Blank);
        for (var i = 0; i < _dirty.Length; i++)
            _dirty[i] = false;
        Array.Copy(_front, _back, _front.Length);
    }

    public List<DirtyCell> GetDirtyRegions()
    {
        var result = new List<DirtyCell>();
        for (var y = 0; y < _height; y++)
        {
            for (var x = 0; x < _width; x++)
            {
                var idx = y * _width + x;
                if (_dirty[idx])
                    result.Add(new DirtyCell(x, y, _back[idx]));
            }
        }
        return result;
    }

    public void ScrollUp(int top, int bottom, int count)
    {
        if (top < 0) top = 0;
        if (bottom >= _height) bottom = _height - 1;
        if (top > bottom) return;
        count = Math.Min(count, bottom - top + 1);
        var width = _width;
        for (var y = top; y <= bottom - count; y++)
        {
            var srcRow = (y + count) * width;
            var dstRow = y * width;
            Array.Copy(_back, srcRow, _back, dstRow, width);
        }
        for (var y = bottom - count + 1; y <= bottom; y++)
        {
            for (var x = 0; x < width; x++)
            {
                _back[y * width + x] = Cell.Blank;
            }
        }
        for (var y = top; y <= bottom; y++)
        {
            for (var x = 0; x < width; x++)
            {
                _dirty[y * width + x] = true;
            }
        }
    }

    public void Resize(int newWidth, int newHeight)
    {
        newWidth = Math.Max(1, newWidth);
        newHeight = Math.Max(1, newHeight);
        if (newWidth == _width && newHeight == _height)
            return;

        var newSize = newWidth * newHeight;
        var newFront = new Cell[newSize];
        var newBack = new Cell[newSize];
        var newDirty = new bool[newSize];

        Array.Fill(newFront, Cell.Blank);
        Array.Fill(newBack, Cell.Blank);

        var copyWidth = Math.Min(_width, newWidth);
        var copyHeight = Math.Min(_height, newHeight);
        for (var y = 0; y < copyHeight; y++)
        {
            for (var x = 0; x < copyWidth; x++)
            {
                var oldIdx = y * _width + x;
                var newIdx = y * newWidth + x;
                newFront[newIdx] = _front[oldIdx];
                newBack[newIdx] = _back[oldIdx];
                newDirty[newIdx] = true;
            }
        }

        _front = newFront;
        _back = newBack;
        _dirty = newDirty;
        _width = newWidth;
        _height = newHeight;
    }

    public Cell[] GetFrontBuffer() => _front;
}
