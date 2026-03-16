namespace Wmux.Core.VtParser;

public enum VtState
{
    Ground,
    Escape,
    EscapeIntermediate,
    CsiEntry,
    CsiParam,
    CsiIntermediate,
    CsiIgnore,
    OscString,
    DcsEntry,
    DcsParam,
    DcsIntermediate,
    DcsPassthrough,
    SosPmApcString
}

public sealed class VtStateMachine
{
    private readonly IVtHandler _handler;
    private VtState _state = VtState.Ground;

    // Parameter accumulation
    private readonly int[] _params = new int[16];
    private int _paramCount;
    private int _currentParam;
    private bool _paramStarted;

    // Intermediate bytes
    private readonly byte[] _intermediates = new byte[4];
    private int _intermediateCount;

    // OSC payload
    private readonly byte[] _oscPayload = new byte[4096];
    private int _oscPayloadLen;

    // UTF-8 decoding for Print
    private int _utf8Codepoint;
    private int _utf8Remaining;

    public VtStateMachine(IVtHandler handler)
    {
        _handler = handler;
    }

    public void Advance(byte b)
    {
        // Anywhere transitions — these override any state
        if (b == 0x18 || b == 0x1A) // CAN, SUB
        {
            _state = VtState.Ground;
            return;
        }

        if (b == 0x1B) // ESC
        {
            // If we were accumulating UTF-8, cancel it
            _utf8Remaining = 0;
            ClearParams();
            _intermediateCount = 0;
            _state = VtState.Escape;
            return;
        }

        switch (_state)
        {
            case VtState.Ground:
                HandleGround(b);
                break;
            case VtState.Escape:
                HandleEscape(b);
                break;
            case VtState.EscapeIntermediate:
                HandleEscapeIntermediate(b);
                break;
            case VtState.CsiEntry:
                HandleCsiEntry(b);
                break;
            case VtState.CsiParam:
                HandleCsiParam(b);
                break;
            case VtState.CsiIntermediate:
                HandleCsiIntermediate(b);
                break;
            case VtState.CsiIgnore:
                HandleCsiIgnore(b);
                break;
            case VtState.OscString:
                HandleOscString(b);
                break;
            case VtState.DcsEntry:
            case VtState.DcsParam:
            case VtState.DcsIntermediate:
            case VtState.DcsPassthrough:
            case VtState.SosPmApcString:
                // For now, consume until ST (ESC \) or BEL
                if (b == 0x07) _state = VtState.Ground;
                break;
        }
    }

    private void HandleGround(byte b)
    {
        if (b < 0x20)
        {
            _handler.Execute(b);
        }
        else if (b == 0x7F)
        {
            // DEL — ignore in ground
        }
        else
        {
            // Printable byte — UTF-8 decode
            HandleUtf8(b);
        }
    }

    private void HandleUtf8(byte b)
    {
        if (_utf8Remaining > 0)
        {
            if ((b & 0xC0) == 0x80) // Continuation byte
            {
                _utf8Codepoint = (_utf8Codepoint << 6) | (b & 0x3F);
                _utf8Remaining--;
                if (_utf8Remaining == 0)
                    _handler.Print(_utf8Codepoint);
            }
            else
            {
                // Invalid continuation — reset and try this byte as a new start
                _utf8Remaining = 0;
                HandleUtf8(b);
            }
            return;
        }

        if (b < 0x80)
        {
            _handler.Print(b);
        }
        else if ((b & 0xE0) == 0xC0)
        {
            _utf8Codepoint = b & 0x1F;
            _utf8Remaining = 1;
        }
        else if ((b & 0xF0) == 0xE0)
        {
            _utf8Codepoint = b & 0x0F;
            _utf8Remaining = 2;
        }
        else if ((b & 0xF8) == 0xF0)
        {
            _utf8Codepoint = b & 0x07;
            _utf8Remaining = 3;
        }
    }

    private void HandleEscape(byte b)
    {
        if (b == '[')
        {
            ClearParams();
            _intermediateCount = 0;
            _state = VtState.CsiEntry;
        }
        else if (b == ']')
        {
            _oscPayloadLen = 0;
            _state = VtState.OscString;
        }
        else if (b == 'P')
        {
            _state = VtState.DcsEntry;
        }
        else if (b == 'X' || b == '^' || b == '_')
        {
            _state = VtState.SosPmApcString;
        }
        else if (b >= 0x20 && b <= 0x2F) // Intermediate
        {
            CollectIntermediate(b);
            _state = VtState.EscapeIntermediate;
        }
        else if (b >= 0x30 && b <= 0x7E) // Final
        {
            _handler.EscDispatch(b, _intermediates.AsSpan(0, _intermediateCount));
            _state = VtState.Ground;
        }
        else if (b < 0x20)
        {
            _handler.Execute(b);
        }
    }

    private void HandleEscapeIntermediate(byte b)
    {
        if (b >= 0x20 && b <= 0x2F)
        {
            CollectIntermediate(b);
        }
        else if (b >= 0x30 && b <= 0x7E)
        {
            _handler.EscDispatch(b, _intermediates.AsSpan(0, _intermediateCount));
            _state = VtState.Ground;
        }
        else if (b < 0x20)
        {
            _handler.Execute(b);
        }
    }

    private void HandleCsiEntry(byte b)
    {
        if (b >= 0x30 && b <= 0x39) // Digit
        {
            _currentParam = b - '0';
            _paramStarted = true;
            _state = VtState.CsiParam;
        }
        else if (b == ';')
        {
            CommitParam();
            _state = VtState.CsiParam;
        }
        else if (b >= 0x3C && b <= 0x3F) // Private marker (<=>?)
        {
            CollectIntermediate(b);
            _state = VtState.CsiParam;
        }
        else if (b >= 0x20 && b <= 0x2F) // Intermediate
        {
            CollectIntermediate(b);
            _state = VtState.CsiIntermediate;
        }
        else if (b >= 0x40 && b <= 0x7E) // Final (no params)
        {
            _handler.CsiDispatch(b, _params.AsSpan(0, _paramCount), _intermediates.AsSpan(0, _intermediateCount));
            _state = VtState.Ground;
        }
        else if (b < 0x20)
        {
            _handler.Execute(b);
        }
    }

    private void HandleCsiParam(byte b)
    {
        if (b >= 0x30 && b <= 0x39) // Digit
        {
            _currentParam = _currentParam * 10 + (b - '0');
            _paramStarted = true;
        }
        else if (b == ';')
        {
            CommitParam();
        }
        else if (b >= 0x20 && b <= 0x2F) // Intermediate
        {
            CommitParam();
            CollectIntermediate(b);
            _state = VtState.CsiIntermediate;
        }
        else if (b >= 0x40 && b <= 0x7E) // Final
        {
            CommitParam();
            _handler.CsiDispatch(b, _params.AsSpan(0, _paramCount), _intermediates.AsSpan(0, _intermediateCount));
            _state = VtState.Ground;
        }
        else if (b >= 0x3C && b <= 0x3F) // Private marker in wrong place
        {
            _state = VtState.CsiIgnore;
        }
        else if (b < 0x20)
        {
            _handler.Execute(b);
        }
    }

    private void HandleCsiIntermediate(byte b)
    {
        if (b >= 0x20 && b <= 0x2F)
        {
            CollectIntermediate(b);
        }
        else if (b >= 0x40 && b <= 0x7E)
        {
            _handler.CsiDispatch(b, _params.AsSpan(0, _paramCount), _intermediates.AsSpan(0, _intermediateCount));
            _state = VtState.Ground;
        }
        else if (b >= 0x30 && b <= 0x3F)
        {
            _state = VtState.CsiIgnore;
        }
        else if (b < 0x20)
        {
            _handler.Execute(b);
        }
    }

    private void HandleCsiIgnore(byte b)
    {
        if (b >= 0x40 && b <= 0x7E)
            _state = VtState.Ground;
        else if (b < 0x20)
            _handler.Execute(b);
    }

    private void HandleOscString(byte b)
    {
        if (b == 0x07) // BEL terminates OSC
        {
            _handler.OscDispatch(_oscPayload.AsSpan(0, _oscPayloadLen));
            _state = VtState.Ground;
        }
        else if (b == 0x5C && _oscPayloadLen > 0 && _oscPayload[_oscPayloadLen - 1] == 0x1B)
        {
            // ST (ESC \) — remove the ESC we already stored
            _oscPayloadLen--;
            _handler.OscDispatch(_oscPayload.AsSpan(0, _oscPayloadLen));
            _state = VtState.Ground;
        }
        else
        {
            if (_oscPayloadLen < _oscPayload.Length)
                _oscPayload[_oscPayloadLen++] = b;
        }
    }

    private void ClearParams()
    {
        _paramCount = 0;
        _currentParam = 0;
        _paramStarted = false;
    }

    private void CommitParam()
    {
        if (_paramCount < _params.Length)
        {
            _params[_paramCount++] = _paramStarted ? _currentParam : 0;
        }
        _currentParam = 0;
        _paramStarted = false;
    }

    private void CollectIntermediate(byte b)
    {
        if (_intermediateCount < _intermediates.Length)
            _intermediates[_intermediateCount++] = b;
    }
}
