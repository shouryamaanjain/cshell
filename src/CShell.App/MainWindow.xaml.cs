using System.Text;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using CShell.App.Controls;
using CShell.Core.Models;
using CShell.Core.Terminal;
using Windows.System;

namespace CShell.App;

public sealed partial class MainWindow : Window
{
    private readonly List<WorkspaceState> _workspaces = new();
    private WorkspaceState? _activeWorkspace;
    private static readonly string UserHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public MainWindow()
    {
        this.InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();

        var appWindow = GetAppWindow();
        if (appWindow != null)
        {
            appWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));
            appWindow.Title = "cshell";
            if (appWindow.TitleBar != null)
            {
                appWindow.TitleBar.BackgroundColor = Windows.UI.Color.FromArgb(255, 20, 20, 20);
                appWindow.TitleBar.ForegroundColor = Windows.UI.Color.FromArgb(255, 224, 224, 224);
                appWindow.TitleBar.InactiveBackgroundColor = Windows.UI.Color.FromArgb(255, 20, 20, 20);
                appWindow.TitleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
                appWindow.TitleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 224, 224, 224);
                appWindow.TitleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(255, 50, 50, 50);
                appWindow.TitleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
            }
        }

        // Ensure keyboard focus is on the root grid after load
        RootGrid.Loaded += (s, e) =>
        {
            RootGrid.Focus(FocusState.Programmatic);
        };

        CreateWorkspace();
    }

    private AppWindow? GetAppWindow()
    {
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        return AppWindow.GetFromWindowId(windowId);
    }

    // ─── Keyboard handling at window level ───

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_activeWorkspace == null) return;
        var session = _activeWorkspace.Panel.Session;

        bool ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        bool shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        bool alt = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        string? keyName = e.Key switch
        {
            VirtualKey.Enter => "Enter",
            VirtualKey.Tab => "Tab",
            VirtualKey.Escape => "Escape",
            VirtualKey.Back => "Backspace",
            VirtualKey.Delete => "Delete",
            VirtualKey.Insert => "Insert",
            VirtualKey.Home => "Home",
            VirtualKey.End => "End",
            VirtualKey.PageUp => "PageUp",
            VirtualKey.PageDown => "PageDown",
            VirtualKey.Up => "Up",
            VirtualKey.Down => "Down",
            VirtualKey.Left => "Left",
            VirtualKey.Right => "Right",
            VirtualKey.F1 => "F1", VirtualKey.F2 => "F2", VirtualKey.F3 => "F3",
            VirtualKey.F4 => "F4", VirtualKey.F5 => "F5", VirtualKey.F6 => "F6",
            VirtualKey.F7 => "F7", VirtualKey.F8 => "F8", VirtualKey.F9 => "F9",
            VirtualKey.F10 => "F10", VirtualKey.F11 => "F11", VirtualKey.F12 => "F12",
            VirtualKey.Space when ctrl => " ",
            _ => null
        };

        if (keyName != null)
        {
            session.SendInput(TerminalInput.EncodeKey(keyName, shift, ctrl, alt,
                session.Emulator.ApplicationCursorKeys));
            e.Handled = true;
            return;
        }

        // Ctrl+letter combos
        if (ctrl && !alt && e.Key >= VirtualKey.A && e.Key <= VirtualKey.Z)
        {
            session.SendInput(TerminalInput.EncodeKey(
                ((char)('a' + e.Key - VirtualKey.A)).ToString(), shift, ctrl, alt));
            e.Handled = true;
            return;
        }
    }

    private void OnRootCharReceived(UIElement sender, CharacterReceivedRoutedEventArgs e)
    {
        if (_activeWorkspace == null) return;

        char ch = (char)e.Character;
        if (ch < 0x20 || ch == 0x7F) return;

        _activeWorkspace.Panel.Session.SendInput(Encoding.UTF8.GetBytes(ch.ToString()));
        e.Handled = true;
    }

    // ─── Workspace management ───

    private void CreateWorkspace()
    {
        string shellPath = GetSelectedShell();

        var workspace = new Workspace();
        var session = new TerminalSession(80, 24);
        var panel = new TerminalPanel(session);
        workspace.Panels[panel.Id] = panel;
        workspace.FocusedPanelId = panel.Id;
        workspace.SplitTree = new PaneNode { Tabs = { panel }, SelectedTab = panel };

        var canvas = new TerminalCanvas(session);
        canvas.Visibility = Visibility.Collapsed; // start hidden

        // Add to tree permanently (never removed)
        TerminalHost.Children.Add(canvas);

        var state = new WorkspaceState
        {
            Workspace = workspace,
            Panel = panel,
            Canvas = canvas
        };

        _workspaces.Add(state);
        WorkspaceList.Items.Add(workspace);
        WorkspaceList.SelectedItem = workspace;

        ActivateWorkspace(state);

        // Start shell with proper working directory
        canvas.StartShell(shellPath, UserHome);

        session.TitleChanged += title =>
            DispatcherQueue.TryEnqueue(() => workspace.Title = title);
        session.DirectoryChanged += dir =>
            DispatcherQueue.TryEnqueue(() => workspace.WorkingDirectory = dir);
    }

    private void ActivateWorkspace(WorkspaceState state)
    {
        // Hide current workspace canvas
        if (_activeWorkspace != null)
            _activeWorkspace.Canvas.Visibility = Visibility.Collapsed;

        _activeWorkspace = state;

        // Show new workspace canvas
        state.Canvas.Visibility = Visibility.Visible;
        state.Canvas.InvalidateCanvas();

        // Restore focus to root grid for keyboard input
        RootGrid.Focus(FocusState.Programmatic);
    }

    private string GetSelectedShell()
    {
        int idx = ShellSelector?.SelectedIndex ?? 0;
        return idx switch
        {
            1 => "cmd.exe",
            2 => "wsl.exe",
            _ => "powershell.exe"
        };
    }

    private void OnNewWorkspace(object sender, RoutedEventArgs e)
    {
        CreateWorkspace();
    }

    private void OnWorkspaceSelected(object sender, SelectionChangedEventArgs e)
    {
        if (WorkspaceList.SelectedItem is Workspace selected)
        {
            var state = _workspaces.Find(w => w.Workspace == selected);
            if (state != null && state != _activeWorkspace)
                ActivateWorkspace(state);
        }
    }

    private sealed class WorkspaceState
    {
        public Workspace Workspace { get; init; } = null!;
        public TerminalPanel Panel { get; init; } = null!;
        public TerminalCanvas Canvas { get; init; } = null!;
    }
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b && b) return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}
