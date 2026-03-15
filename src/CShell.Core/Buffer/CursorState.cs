namespace CShell.Core.Buffer;

public enum CursorShape
{
    Block,
    Underline,
    Bar
}

public struct CursorState
{
    public int Row;
    public int Col;
    public bool Visible;
    public CursorShape Shape;
    public uint ForegroundRgb;
    public uint BackgroundRgb;
    public CellAttributes Attributes;

    public static CursorState Default(uint fg, uint bg) => new()
    {
        Row = 0,
        Col = 0,
        Visible = true,
        Shape = CursorShape.Block,
        ForegroundRgb = fg,
        BackgroundRgb = bg,
        Attributes = CellAttributes.None
    };
}
