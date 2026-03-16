using System.ComponentModel;

namespace Wmux.Core.Models;

public sealed class Workspace : INotifyPropertyChanged
{
    private string _title = "Terminal";
    private string? _customTitle;
    private string _workingDirectory = "";
    private string? _gitBranch;
    private string? _notificationText;
    private bool _hasUnread;
    private Guid? _focusedPanelId;

    public Guid Id { get; } = Guid.NewGuid();
    public Dictionary<Guid, IPanel> Panels { get; } = new();
    public SplitNode? SplitTree { get; set; }

    public string Title
    {
        get => _customTitle ?? _title;
        set { _title = value; OnPropertyChanged(nameof(Title)); }
    }

    public string? CustomTitle
    {
        get => _customTitle;
        set { _customTitle = value; OnPropertyChanged(nameof(Title)); OnPropertyChanged(nameof(CustomTitle)); }
    }

    public string WorkingDirectory
    {
        get => _workingDirectory;
        set { _workingDirectory = value; OnPropertyChanged(nameof(WorkingDirectory)); }
    }

    public string? GitBranch
    {
        get => _gitBranch;
        set { _gitBranch = value; OnPropertyChanged(nameof(GitBranch)); }
    }

    public string? NotificationText
    {
        get => _notificationText;
        set { _notificationText = value; OnPropertyChanged(nameof(NotificationText)); OnPropertyChanged(nameof(HasNotification)); }
    }

    public bool HasNotification => !string.IsNullOrEmpty(_notificationText);

    public bool HasUnread
    {
        get => _hasUnread;
        set { _hasUnread = value; OnPropertyChanged(nameof(HasUnread)); }
    }

    public Guid? FocusedPanelId
    {
        get => _focusedPanelId;
        set { _focusedPanelId = value; OnPropertyChanged(nameof(FocusedPanelId)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
