using CShell.Core.Buffer;
using CShell.Core.VtParser;

namespace CShell.Core.Terminal;

public sealed class TerminalEmulator : IVtHandler
{
    private readonly TerminalBuffer _buffer;
    private readonly OscParser _oscParser;

    // Mode flags
    private bool _applicationCursorKeys;
    private bool _autoWrapMode = true;
    private bool _bracketedPasteMode;
    private bool _originMode;
    private bool _wrapNext; // Deferred wrap flag

    public bool ApplicationCursorKeys => _applicationCursorKeys;
    public bool BracketedPasteMode => _bracketedPasteMode;
    public event Action<string>? TitleChanged;

    public TerminalEmulator(TerminalBuffer buffer, OscParser oscParser)
    {
        _buffer = buffer;
        _oscParser = oscParser;
    }

    public void Print(int codepoint)
    {
        if (_wrapNext && _autoWrapMode)
        {
            _buffer.GetLine(_buffer.Cursor.Row).IsWrapped = true;
            _buffer.Cursor.Col = 0;
            if (_buffer.Cursor.Row == _buffer.ScrollBottom)
                _buffer.ScrollUp();
            else if (_buffer.Cursor.Row < _buffer.Rows - 1)
                _buffer.Cursor.Row++;
            _wrapNext = false;
        }

        var cell = new TerminalCell
        {
            Codepoint = codepoint,
            Width = 1,
            ForegroundRgb = _buffer.Cursor.ForegroundRgb,
            BackgroundRgb = _buffer.Cursor.BackgroundRgb,
            Attributes = _buffer.Cursor.Attributes
        };

        _buffer.SetCell(_buffer.Cursor.Row, _buffer.Cursor.Col, cell);

        if (_buffer.Cursor.Col >= _buffer.Columns - 1)
            _wrapNext = true;
        else
            _buffer.Cursor.Col++;
    }

    public void Execute(byte controlCode)
    {
        _wrapNext = false;
        switch (controlCode)
        {
            case 0x07: // BEL
                break;
            case 0x08: // BS (Backspace)
                if (_buffer.Cursor.Col > 0)
                    _buffer.Cursor.Col--;
                break;
            case 0x09: // HT (Tab)
                _buffer.Cursor.Col = Math.Min((_buffer.Cursor.Col / 8 + 1) * 8, _buffer.Columns - 1);
                break;
            case 0x0A: // LF (Line Feed)
            case 0x0B: // VT (Vertical Tab)
            case 0x0C: // FF (Form Feed)
                LineFeed();
                break;
            case 0x0D: // CR (Carriage Return)
                _buffer.Cursor.Col = 0;
                break;
        }
    }

    public void CsiDispatch(byte final, ReadOnlySpan<int> parameters, ReadOnlySpan<byte> intermediates)
    {
        _wrapNext = false;

        // Check for private mode marker
        bool isPrivate = intermediates.Length > 0 && intermediates[0] == '?';

        int p0 = parameters.Length > 0 ? parameters[0] : 0;
        int p1 = parameters.Length > 1 ? parameters[1] : 0;

        switch (final)
        {
            case (byte)'A': // CUU - Cursor Up
                _buffer.Cursor.Row = Math.Max(_buffer.ScrollTop, _buffer.Cursor.Row - Math.Max(p0, 1));
                break;
            case (byte)'B': // CUD - Cursor Down
                _buffer.Cursor.Row = Math.Min(_buffer.ScrollBottom, _buffer.Cursor.Row + Math.Max(p0, 1));
                break;
            case (byte)'C': // CUF - Cursor Forward
                _buffer.Cursor.Col = Math.Min(_buffer.Columns - 1, _buffer.Cursor.Col + Math.Max(p0, 1));
                break;
            case (byte)'D': // CUB - Cursor Backward
                _buffer.Cursor.Col = Math.Max(0, _buffer.Cursor.Col - Math.Max(p0, 1));
                break;
            case (byte)'E': // CNL - Cursor Next Line
                _buffer.Cursor.Col = 0;
                _buffer.Cursor.Row = Math.Min(_buffer.ScrollBottom, _buffer.Cursor.Row + Math.Max(p0, 1));
                break;
            case (byte)'F': // CPL - Cursor Previous Line
                _buffer.Cursor.Col = 0;
                _buffer.Cursor.Row = Math.Max(_buffer.ScrollTop, _buffer.Cursor.Row - Math.Max(p0, 1));
                break;
            case (byte)'G': // CHA - Cursor Horizontal Absolute
                _buffer.Cursor.Col = Math.Clamp(Math.Max(p0, 1) - 1, 0, _buffer.Columns - 1);
                break;
            case (byte)'H': // CUP - Cursor Position
            case (byte)'f': // HVP - Horizontal Vertical Position
                _buffer.Cursor.Row = Math.Clamp(Math.Max(p0, 1) - 1, 0, _buffer.Rows - 1);
                _buffer.Cursor.Col = Math.Clamp(Math.Max(p1, 1) - 1, 0, _buffer.Columns - 1);
                break;
            case (byte)'J': // ED - Erase in Display
                _buffer.EraseInDisplay(p0);
                break;
            case (byte)'K': // EL - Erase in Line
                _buffer.EraseInLine(p0);
                break;
            case (byte)'L': // IL - Insert Lines
                _buffer.InsertLines(_buffer.Cursor.Row, Math.Max(p0, 1));
                break;
            case (byte)'M': // DL - Delete Lines
                _buffer.DeleteLines(_buffer.Cursor.Row, Math.Max(p0, 1));
                break;
            case (byte)'P': // DCH - Delete Characters
                DeleteCharacters(Math.Max(p0, 1));
                break;
            case (byte)'@': // ICH - Insert Characters
                InsertCharacters(Math.Max(p0, 1));
                break;
            case (byte)'S': // SU - Scroll Up
                _buffer.ScrollUp(Math.Max(p0, 1));
                break;
            case (byte)'T': // SD - Scroll Down
                _buffer.ScrollDown(Math.Max(p0, 1));
                break;
            case (byte)'X': // ECH - Erase Characters
                EraseCharacters(Math.Max(p0, 1));
                break;
            case (byte)'d': // VPA - Vertical Position Absolute
                _buffer.Cursor.Row = Math.Clamp(Math.Max(p0, 1) - 1, 0, _buffer.Rows - 1);
                break;
            case (byte)'m': // SGR - Select Graphic Rendition
                HandleSgr(parameters);
                break;
            case (byte)'r': // DECSTBM - Set Scrolling Region
                int top = Math.Max(p0, 1) - 1;
                int bottom = (p1 == 0 ? _buffer.Rows : p1) - 1;
                _buffer.ScrollTop = Math.Clamp(top, 0, _buffer.Rows - 1);
                _buffer.ScrollBottom = Math.Clamp(bottom, _buffer.ScrollTop, _buffer.Rows - 1);
                _buffer.Cursor.Row = _originMode ? _buffer.ScrollTop : 0;
                _buffer.Cursor.Col = 0;
                break;
            case (byte)'h': // SM/DECSET - Set Mode
                SetMode(parameters, isPrivate, true);
                break;
            case (byte)'l': // RM/DECRST - Reset Mode
                SetMode(parameters, isPrivate, false);
                break;
            case (byte)'n': // DSR - Device Status Report
                // Handled by the session (needs to write back to ConPTY)
                break;
            case (byte)'c': // DA - Device Attributes
                // Handled by the session
                break;
            case (byte)'t': // Window manipulation — ignore
                break;
        }
    }

    public void EscDispatch(byte final, ReadOnlySpan<byte> intermediates)
    {
        _wrapNext = false;
        if (intermediates.Length > 0)
        {
            // ESC # 8 = DECALN (fill with 'E')
            if (intermediates[0] == '#' && final == '8')
            {
                for (int r = 0; r < _buffer.Rows; r++)
                    for (int c = 0; c < _buffer.Columns; c++)
                        _buffer.SetCell(r, c, new TerminalCell { Codepoint = 'E', Width = 1, ForegroundRgb = _buffer.DefaultFg, BackgroundRgb = _buffer.DefaultBg });
            }
            return;
        }

        switch (final)
        {
            case (byte)'7': // DECSC - Save Cursor
                _buffer.SaveCursor();
                break;
            case (byte)'8': // DECRC - Restore Cursor
                _buffer.RestoreCursor();
                break;
            case (byte)'D': // IND - Index (move down, scroll if at bottom)
                LineFeed();
                break;
            case (byte)'M': // RI - Reverse Index
                if (_buffer.Cursor.Row == _buffer.ScrollTop)
                    _buffer.ScrollDown();
                else if (_buffer.Cursor.Row > 0)
                    _buffer.Cursor.Row--;
                break;
            case (byte)'E': // NEL - Next Line
                _buffer.Cursor.Col = 0;
                LineFeed();
                break;
            case (byte)'c': // RIS - Full Reset
                _buffer.Cursor = CursorState.Default(_buffer.DefaultFg, _buffer.DefaultBg);
                _buffer.ScrollTop = 0;
                _buffer.ScrollBottom = _buffer.Rows - 1;
                _buffer.EraseInDisplay(2);
                _applicationCursorKeys = false;
                _autoWrapMode = true;
                _bracketedPasteMode = false;
                _originMode = false;
                break;
        }
    }

    public void OscDispatch(ReadOnlySpan<byte> payload)
    {
        // Check for title setting: OSC 0;title or OSC 2;title
        if (payload.Length >= 2)
        {
            int semicolonIdx = payload.IndexOf((byte)';');
            if (semicolonIdx == 1 && (payload[0] == '0' || payload[0] == '2'))
            {
                var titleBytes = payload[(semicolonIdx + 1)..];
                var title = System.Text.Encoding.UTF8.GetString(titleBytes);
                TitleChanged?.Invoke(title);
                return;
            }
        }
        _oscParser.Parse(payload);
    }

    private void LineFeed()
    {
        if (_buffer.Cursor.Row == _buffer.ScrollBottom)
            _buffer.ScrollUp();
        else if (_buffer.Cursor.Row < _buffer.Rows - 1)
            _buffer.Cursor.Row++;
    }

    private void DeleteCharacters(int count)
    {
        var line = _buffer.GetLine(_buffer.Cursor.Row);
        int col = _buffer.Cursor.Col;
        for (int c = col; c < _buffer.Columns; c++)
        {
            if (c + count < _buffer.Columns)
                line[c] = line[c + count];
            else
                line[c] = TerminalCell.Empty(_buffer.DefaultFg, _buffer.DefaultBg);
        }
        line.IsDirty = true;
        _buffer.IsDirty = true;
    }

    private void InsertCharacters(int count)
    {
        var line = _buffer.GetLine(_buffer.Cursor.Row);
        int col = _buffer.Cursor.Col;
        for (int c = _buffer.Columns - 1; c >= col; c--)
        {
            if (c - count >= col)
                line[c] = line[c - count];
            else
                line[c] = TerminalCell.Empty(_buffer.DefaultFg, _buffer.DefaultBg);
        }
        line.IsDirty = true;
        _buffer.IsDirty = true;
    }

    private void EraseCharacters(int count)
    {
        var line = _buffer.GetLine(_buffer.Cursor.Row);
        int end = Math.Min(_buffer.Cursor.Col + count, _buffer.Columns);
        line.ClearRange(_buffer.Cursor.Col, end, _buffer.DefaultFg, _buffer.DefaultBg);
        _buffer.IsDirty = true;
    }

    private void HandleSgr(ReadOnlySpan<int> parameters)
    {
        if (parameters.Length == 0)
        {
            ResetSgr();
            return;
        }

        for (int i = 0; i < parameters.Length; i++)
        {
            int p = parameters[i];
            switch (p)
            {
                case 0: ResetSgr(); break;
                case 1: _buffer.Cursor.Attributes |= CellAttributes.Bold; break;
                case 2: _buffer.Cursor.Attributes |= CellAttributes.Dim; break;
                case 3: _buffer.Cursor.Attributes |= CellAttributes.Italic; break;
                case 4: _buffer.Cursor.Attributes |= CellAttributes.Underline; break;
                case 5: _buffer.Cursor.Attributes |= CellAttributes.Blink; break;
                case 7: _buffer.Cursor.Attributes |= CellAttributes.Inverse; break;
                case 8: _buffer.Cursor.Attributes |= CellAttributes.Hidden; break;
                case 9: _buffer.Cursor.Attributes |= CellAttributes.Strikethrough; break;
                case 21: _buffer.Cursor.Attributes |= CellAttributes.DoubleUnderline; break;
                case 22: _buffer.Cursor.Attributes &= ~(CellAttributes.Bold | CellAttributes.Dim); break;
                case 23: _buffer.Cursor.Attributes &= ~CellAttributes.Italic; break;
                case 24: _buffer.Cursor.Attributes &= ~(CellAttributes.Underline | CellAttributes.DoubleUnderline); break;
                case 25: _buffer.Cursor.Attributes &= ~CellAttributes.Blink; break;
                case 27: _buffer.Cursor.Attributes &= ~CellAttributes.Inverse; break;
                case 28: _buffer.Cursor.Attributes &= ~CellAttributes.Hidden; break;
                case 29: _buffer.Cursor.Attributes &= ~CellAttributes.Strikethrough; break;

                // Foreground colors (standard)
                case >= 30 and <= 37:
                    _buffer.Cursor.ForegroundRgb = ColorPalette.GetAnsiColor(p - 30);
                    break;
                case 38: // Extended foreground
                    i = ParseExtendedColor(parameters, i, out uint fg);
                    _buffer.Cursor.ForegroundRgb = fg;
                    break;
                case 39: // Default foreground
                    _buffer.Cursor.ForegroundRgb = _buffer.DefaultFg;
                    break;

                // Background colors (standard)
                case >= 40 and <= 47:
                    _buffer.Cursor.BackgroundRgb = ColorPalette.GetAnsiColor(p - 40);
                    break;
                case 48: // Extended background
                    i = ParseExtendedColor(parameters, i, out uint bg);
                    _buffer.Cursor.BackgroundRgb = bg;
                    break;
                case 49: // Default background
                    _buffer.Cursor.BackgroundRgb = _buffer.DefaultBg;
                    break;

                // Bright foreground
                case >= 90 and <= 97:
                    _buffer.Cursor.ForegroundRgb = ColorPalette.GetAnsiColor(p - 90 + 8);
                    break;

                // Bright background
                case >= 100 and <= 107:
                    _buffer.Cursor.BackgroundRgb = ColorPalette.GetAnsiColor(p - 100 + 8);
                    break;
            }
        }
    }

    private static int ParseExtendedColor(ReadOnlySpan<int> parameters, int i, out uint color)
    {
        color = ColorPalette.DefaultForeground;
        if (i + 1 < parameters.Length)
        {
            int mode = parameters[i + 1];
            if (mode == 5 && i + 2 < parameters.Length)
            {
                color = ColorPalette.Get256Color(parameters[i + 2]);
                return i + 2;
            }
            if (mode == 2 && i + 4 < parameters.Length)
            {
                color = ColorPalette.FromRgb(
                    Math.Clamp(parameters[i + 2], 0, 255),
                    Math.Clamp(parameters[i + 3], 0, 255),
                    Math.Clamp(parameters[i + 4], 0, 255));
                return i + 4;
            }
        }
        return i;
    }

    private void ResetSgr()
    {
        _buffer.Cursor.Attributes = CellAttributes.None;
        _buffer.Cursor.ForegroundRgb = _buffer.DefaultFg;
        _buffer.Cursor.BackgroundRgb = _buffer.DefaultBg;
    }

    private void SetMode(ReadOnlySpan<int> parameters, bool isPrivate, bool enable)
    {
        foreach (int p in parameters)
        {
            if (isPrivate)
            {
                switch (p)
                {
                    case 1: _applicationCursorKeys = enable; break; // DECCKM
                    case 6: _originMode = enable; break; // DECOM
                    case 7: _autoWrapMode = enable; break; // DECAWM
                    case 25: _buffer.Cursor.Visible = enable; break; // DECTCEM
                    case 1049: // Alt screen with save/restore cursor
                        if (enable) _buffer.SwitchToAlternateScreen();
                        else _buffer.SwitchToMainScreen();
                        break;
                    case 47: // Alt screen (no cursor save)
                    case 1047:
                        if (enable) _buffer.SwitchToAlternateScreen();
                        else _buffer.SwitchToMainScreen();
                        break;
                    case 2004: _bracketedPasteMode = enable; break; // Bracketed paste
                }
            }
        }
    }
}
