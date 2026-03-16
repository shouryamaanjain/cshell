using System.ComponentModel;

namespace Wmux.Core.Models;

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
