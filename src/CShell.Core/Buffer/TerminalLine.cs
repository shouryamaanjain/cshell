namespace CShell.Core.Buffer;

public sealed class TerminalLine
{
    private TerminalCell[] _cells;
    public bool IsDirty { get; set; } = true;
    public bool IsWrapped { get; set; }

    public int Length => _cells.Length;

    public TerminalLine(int columns, uint defaultFg, uint defaultBg)
    {
        _cells = new TerminalCell[columns];
        for (int i = 0; i < columns; i++)
            _cells[i] = TerminalCell.Empty(defaultFg, defaultBg);
    }

    public ref TerminalCell this[int col] => ref _cells[col];

    public void Resize(int newColumns, uint defaultFg, uint defaultBg)
    {
        var newCells = new TerminalCell[newColumns];
        int copyLen = Math.Min(_cells.Length, newColumns);
        Array.Copy(_cells, newCells, copyLen);
        for (int i = copyLen; i < newColumns; i++)
            newCells[i] = TerminalCell.Empty(defaultFg, defaultBg);
        _cells = newCells;
        IsDirty = true;
    }

    public void Clear(uint fg, uint bg)
    {
        for (int i = 0; i < _cells.Length; i++)
            _cells[i] = TerminalCell.Empty(fg, bg);
        IsDirty = true;
        IsWrapped = false;
    }

    public void ClearRange(int start, int end, uint fg, uint bg)
    {
        for (int i = start; i < end && i < _cells.Length; i++)
            _cells[i] = TerminalCell.Empty(fg, bg);
        IsDirty = true;
    }

    public TerminalLine Clone()
    {
        var clone = new TerminalLine(_cells.Length, 0, 0)
        {
            IsWrapped = IsWrapped
        };
        Array.Copy(_cells, clone._cells, _cells.Length);
        return clone;
    }
}
