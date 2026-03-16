namespace Wmux.Core.Buffer;

public enum SelectionMode
{
    None,
    Character,
    Word,
    Line
}

public sealed class SelectionModel
{
    public SelectionMode Mode { get; set; } = SelectionMode.None;
    public int AnchorRow { get; set; }
    public int AnchorCol { get; set; }
    public int EndRow { get; set; }
    public int EndCol { get; set; }

    public bool IsActive => Mode != SelectionMode.None;

    public void Start(int row, int col, SelectionMode mode = SelectionMode.Character)
    {
        Mode = mode;
        AnchorRow = row;
        AnchorCol = col;
        EndRow = row;
        EndCol = col;
    }

    public void Update(int row, int col)
    {
        EndRow = row;
        EndCol = col;
    }

    public void Clear()
    {
        Mode = SelectionMode.None;
    }

    public bool Contains(int row, int col)
    {
        if (!IsActive) return false;

        GetOrderedRange(out int startRow, out int startCol, out int endRow, out int endCol);

        if (Mode == SelectionMode.Line)
            return row >= startRow && row <= endRow;

        if (row < startRow || row > endRow) return false;
        if (row == startRow && row == endRow) return col >= startCol && col <= endCol;
        if (row == startRow) return col >= startCol;
        if (row == endRow) return col <= endCol;
        return true;
    }

    public void GetOrderedRange(out int startRow, out int startCol, out int endRow, out int endCol)
    {
        if (AnchorRow < EndRow || (AnchorRow == EndRow && AnchorCol <= EndCol))
        {
            startRow = AnchorRow;
            startCol = AnchorCol;
            endRow = EndRow;
            endCol = EndCol;
        }
        else
        {
            startRow = EndRow;
            startCol = EndCol;
            endRow = AnchorRow;
            endCol = AnchorCol;
        }
    }
}
