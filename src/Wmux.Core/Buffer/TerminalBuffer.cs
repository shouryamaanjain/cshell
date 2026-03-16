namespace Wmux.Core.Buffer;

public sealed class TerminalBuffer
{
    private TerminalLine[] _lines;
    private TerminalLine[]? _altLines;
    private CursorState _savedCursor;
    private CursorState _altSavedCursor;

    public int Rows { get; private set; }
    public int Columns { get; private set; }
    public CursorState Cursor;
    public SelectionModel Selection { get; } = new();

    // Scroll region (1-based in VT, 0-based here)
    public int ScrollTop { get; set; }
    public int ScrollBottom { get; set; }

    public bool IsAlternateScreen { get; private set; }

    // Scrollback
    private readonly List<TerminalLine> _scrollback = new();
    public int ScrollbackLimit { get; }
    public int ScrollOffset { get; set; } // 0 = bottom (live), positive = scrolled up
    public int ScrollbackCount => _scrollback.Count;

    // Dirty tracking
    public bool IsDirty { get; set; } = true;

    // Default colors
    public uint DefaultFg { get; set; } = ColorPalette.DefaultForeground;
    public uint DefaultBg { get; set; } = ColorPalette.DefaultBackground;

    public TerminalBuffer(int columns, int rows, int scrollbackLimit = 10_000)
    {
        Columns = columns;
        Rows = rows;
        ScrollbackLimit = scrollbackLimit;
        ScrollBottom = rows - 1;
        Cursor = CursorState.Default(DefaultFg, DefaultBg);

        _lines = new TerminalLine[rows];
        for (int i = 0; i < rows; i++)
            _lines[i] = new TerminalLine(columns, DefaultFg, DefaultBg);
    }

    public TerminalLine GetLine(int row) => _lines[row];

    public TerminalLine GetVisibleLine(int row)
    {
        if (ScrollOffset > 0)
        {
            int scrollbackRow = _scrollback.Count - ScrollOffset + row;
            if (scrollbackRow >= 0 && scrollbackRow < _scrollback.Count)
                return _scrollback[scrollbackRow];
            int activeRow = row - (ScrollOffset - Math.Min(ScrollOffset, _scrollback.Count - (_scrollback.Count - ScrollOffset + row >= 0 ? 0 : 0)));
        }
        return _lines[row];
    }

    public ref TerminalCell GetCell(int row, int col) => ref _lines[row][col];

    public void SetCell(int row, int col, TerminalCell cell)
    {
        if (row >= 0 && row < Rows && col >= 0 && col < Columns)
        {
            _lines[row][col] = cell;
            _lines[row].IsDirty = true;
            IsDirty = true;
        }
    }

    public void ScrollUp(int count = 1)
    {
        for (int i = 0; i < count; i++)
        {
            // Move top line of scroll region to scrollback (only for main screen)
            if (!IsAlternateScreen && ScrollTop == 0)
            {
                _scrollback.Add(_lines[ScrollTop].Clone());
                while (_scrollback.Count > ScrollbackLimit)
                    _scrollback.RemoveAt(0);
            }

            // Shift lines up within scroll region
            for (int row = ScrollTop; row < ScrollBottom; row++)
                _lines[row] = _lines[row + 1];

            _lines[ScrollBottom] = new TerminalLine(Columns, DefaultFg, DefaultBg);
        }
        MarkAllDirty();
    }

    public void ScrollDown(int count = 1)
    {
        for (int i = 0; i < count; i++)
        {
            for (int row = ScrollBottom; row > ScrollTop; row--)
                _lines[row] = _lines[row - 1];

            _lines[ScrollTop] = new TerminalLine(Columns, DefaultFg, DefaultBg);
        }
        MarkAllDirty();
    }

    public void InsertLines(int row, int count)
    {
        int bottom = ScrollBottom;
        for (int i = 0; i < count && row <= bottom; i++)
        {
            for (int r = bottom; r > row; r--)
                _lines[r] = _lines[r - 1];
            _lines[row] = new TerminalLine(Columns, DefaultFg, DefaultBg);
        }
        MarkAllDirty();
    }

    public void DeleteLines(int row, int count)
    {
        int bottom = ScrollBottom;
        for (int i = 0; i < count && row <= bottom; i++)
        {
            for (int r = row; r < bottom; r++)
                _lines[r] = _lines[r + 1];
            _lines[bottom] = new TerminalLine(Columns, DefaultFg, DefaultBg);
        }
        MarkAllDirty();
    }

    public void EraseInDisplay(int mode)
    {
        switch (mode)
        {
            case 0: // Below cursor
                _lines[Cursor.Row].ClearRange(Cursor.Col, Columns, DefaultFg, DefaultBg);
                for (int r = Cursor.Row + 1; r < Rows; r++)
                    _lines[r].Clear(DefaultFg, DefaultBg);
                break;
            case 1: // Above cursor
                for (int r = 0; r < Cursor.Row; r++)
                    _lines[r].Clear(DefaultFg, DefaultBg);
                _lines[Cursor.Row].ClearRange(0, Cursor.Col + 1, DefaultFg, DefaultBg);
                break;
            case 2: // Entire display
                for (int r = 0; r < Rows; r++)
                    _lines[r].Clear(DefaultFg, DefaultBg);
                break;
            case 3: // Entire display + scrollback
                _scrollback.Clear();
                for (int r = 0; r < Rows; r++)
                    _lines[r].Clear(DefaultFg, DefaultBg);
                break;
        }
        MarkAllDirty();
    }

    public void EraseInLine(int mode)
    {
        switch (mode)
        {
            case 0: // Right of cursor
                _lines[Cursor.Row].ClearRange(Cursor.Col, Columns, DefaultFg, DefaultBg);
                break;
            case 1: // Left of cursor
                _lines[Cursor.Row].ClearRange(0, Cursor.Col + 1, DefaultFg, DefaultBg);
                break;
            case 2: // Entire line
                _lines[Cursor.Row].Clear(DefaultFg, DefaultBg);
                break;
        }
        IsDirty = true;
    }

    public void SwitchToAlternateScreen()
    {
        if (IsAlternateScreen) return;
        _savedCursor = Cursor;
        _altLines = _lines;
        _lines = new TerminalLine[Rows];
        for (int i = 0; i < Rows; i++)
            _lines[i] = new TerminalLine(Columns, DefaultFg, DefaultBg);
        IsAlternateScreen = true;
        ScrollTop = 0;
        ScrollBottom = Rows - 1;
        MarkAllDirty();
    }

    public void SwitchToMainScreen()
    {
        if (!IsAlternateScreen) return;
        _lines = _altLines!;
        _altLines = null;
        Cursor = _savedCursor;
        IsAlternateScreen = false;
        ScrollTop = 0;
        ScrollBottom = Rows - 1;
        MarkAllDirty();
    }

    public void SaveCursor() => _savedCursor = Cursor;
    public void RestoreCursor() => Cursor = _savedCursor;

    public void Resize(int newCols, int newRows)
    {
        var newLines = new TerminalLine[newRows];
        int copyRows = Math.Min(Rows, newRows);
        for (int i = 0; i < copyRows; i++)
        {
            newLines[i] = _lines[i];
            newLines[i].Resize(newCols, DefaultFg, DefaultBg);
        }
        for (int i = copyRows; i < newRows; i++)
            newLines[i] = new TerminalLine(newCols, DefaultFg, DefaultBg);

        _lines = newLines;
        Rows = newRows;
        Columns = newCols;
        ScrollTop = 0;
        ScrollBottom = newRows - 1;

        // Clamp cursor
        if (Cursor.Row >= Rows) Cursor.Row = Rows - 1;
        if (Cursor.Col >= Columns) Cursor.Col = Columns - 1;

        MarkAllDirty();
    }

    public void MarkAllDirty()
    {
        for (int i = 0; i < Rows; i++)
            _lines[i].IsDirty = true;
        IsDirty = true;
    }

    public void ClearDirty()
    {
        for (int i = 0; i < Rows; i++)
            _lines[i].IsDirty = false;
        IsDirty = false;
    }
}
