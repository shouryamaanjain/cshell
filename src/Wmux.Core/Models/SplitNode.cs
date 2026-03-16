namespace Wmux.Core.Models;

public enum SplitOrientation
{
    Horizontal,
    Vertical
}

public abstract class SplitNode { }

public sealed class PaneNode : SplitNode
{
    public Guid Id { get; } = Guid.NewGuid();
    public List<IPanel> Tabs { get; } = new();
    public IPanel? SelectedTab { get; set; }
}

public sealed class SplitBranchNode : SplitNode
{
    public SplitOrientation Orientation { get; set; }
    public double DividerPosition { get; set; } = 0.5;
    public SplitNode First { get; set; } = null!;
    public SplitNode Second { get; set; } = null!;
}
