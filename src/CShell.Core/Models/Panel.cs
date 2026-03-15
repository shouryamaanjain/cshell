using System.ComponentModel;

namespace CShell.Core.Models;

public enum PanelType
{
    Terminal,
    Browser,
    Markdown
}

public interface IPanel : INotifyPropertyChanged
{
    Guid Id { get; }
    PanelType PanelType { get; }
    string DisplayTitle { get; }
    void Focus();
    void Unfocus();
    void Close();
}
