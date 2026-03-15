namespace CShell.Core.Buffer;

[Flags]
public enum CellAttributes : ushort
{
    None = 0,
    Bold = 1,
    Italic = 2,
    Underline = 4,
    Strikethrough = 8,
    Dim = 16,
    Inverse = 32,
    Hidden = 64,
    Blink = 128,
    DoubleUnderline = 256,
}

public struct TerminalCell
{
    public int Codepoint;
    public byte Width;           // 1 = normal, 2 = wide (CJK)
    public uint ForegroundRgb;   // 0xFF_RRGGBB packed
    public uint BackgroundRgb;
    public CellAttributes Attributes;

    public static TerminalCell Empty(uint fg, uint bg) => new()
    {
        Codepoint = ' ',
        Width = 1,
        ForegroundRgb = fg,
        BackgroundRgb = bg,
        Attributes = CellAttributes.None
    };
}
