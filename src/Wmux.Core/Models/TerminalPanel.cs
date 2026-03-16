using System.ComponentModel;
using Wmux.Core.Terminal;

namespace Wmux.Core.Models;

public sealed class TerminalPanel : IPanel
{
    private string _title = "Terminal";

    public Guid Id { get; } = Guid.NewGuid();
    public PanelType PanelType => PanelType.Terminal;
    public TerminalSession Session { get; }

    public string DisplayTitle
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayTitle)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public TerminalPanel(TerminalSession session)
    {
        Session = session;
        session.TitleChanged += title => DisplayTitle = title;
    }

    public void Focus() { }
    public void Unfocus() { }

    public void Close()
    {
        Session.Dispose();
    }
}
