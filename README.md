<h1 align="center">cshell</h1>
<p align="center">A native Windows terminal with vertical tabs and workspaces, built for developers running AI coding agents</p>

<p align="center">
  <img src="https://img.shields.io/badge/platform-Windows%2010%2B-blue" alt="Windows 10+" />
  <img src="https://img.shields.io/badge/.NET-8.0-purple" alt=".NET 8" />
  <img src="https://img.shields.io/badge/UI-WinUI%203-green" alt="WinUI 3" />
  <img src="https://img.shields.io/github/license/shouryamaanjain/cshell" alt="MIT License" />
</p>

<p align="center">
  <a href="#features">Features</a> &bull;
  <a href="#install">Install</a> &bull;
  <a href="#build-from-source">Build</a> &bull;
  <a href="#architecture">Architecture</a> &bull;
  <a href="#roadmap">Roadmap</a> &bull;
  <a href="#contributing">Contributing</a>
</p>

---

## What is cshell?

cshell is a Windows-native terminal application inspired by [cmux](https://github.com/manaflow-ai/cmux). It gives you vertical tabs, workspaces, and a dark minimal UI — all built natively with WinUI 3 and C#, not Electron.

Run PowerShell, CMD, and WSL side by side in separate workspaces. Switch between them instantly from the sidebar.

## Features

- **Vertical sidebar** with workspace list — see all your terminals at a glance
- **Multiple shells** — PowerShell, CMD, and WSL support out of the box
- **GPU-accelerated rendering** — terminal output rendered via Win2D / Direct2D
- **Full VT100/xterm support** — 16/256/truecolor, bold, italic, underline, alternate screen buffer
- **Native Windows app** — WinUI 3 + .NET 8, Mica backdrop, custom dark theme
- **ConPTY integration** — uses the modern Windows Pseudo Console API
- **Workspace management** — create, switch, and manage independent terminal workspaces

## Install

### Download

Download the latest release from the [Releases](https://github.com/shouryamaanjain/cshell/releases) page.

### Build from source

**Prerequisites:**
- Windows 10 (1903+) or Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows App SDK (restored automatically via NuGet)

```bash
git clone https://github.com/shouryamaanjain/cshell.git
cd cshell
dotnet build src/CShell.App/CShell.App.csproj -c Release -p:Platform=x64
```

Run:
```bash
dotnet run --project src/CShell.App/CShell.App.csproj -c Release
```

Or launch the built executable directly:
```
src\CShell.App\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\CShell.App.exe
```

## Architecture

cshell is structured as four projects:

```
cshell/
├── src/
│   ├── CShell.Core/          # Terminal engine (no UI dependencies)
│   │   ├── ConPty/            # Windows Pseudo Console (ConPTY) P/Invoke wrapper
│   │   ├── VtParser/          # VT100/xterm escape sequence state machine
│   │   ├── Buffer/            # Terminal cell grid, scrollback, cursor, selection
│   │   ├── Terminal/          # Session orchestrator, emulator, keyboard input encoding
│   │   └── Models/            # Workspace, Panel, SplitNode, Notification
│   ├── CShell.Services/       # Application services (workspace management)
│   ├── CShell.App/            # WinUI 3 application
│   │   ├── Controls/          # TerminalCanvas — Win2D GPU-accelerated renderer
│   │   ├── Themes/            # Dark theme resources
│   │   └── MainWindow         # Window shell with sidebar + terminal host
│   └── CShell.Cli/            # CLI tool (planned)
└── cshell.sln
```

### Key technical decisions

| Component | Choice | Why |
|-----------|--------|-----|
| UI Framework | WinUI 3 / Windows App SDK | Native Windows, Mica backdrop, dark mode, no Electron |
| Terminal Rendering | Win2D (Direct2D + DirectWrite) | GPU-accelerated, proper font shaping for monospace grids |
| Process Spawning | ConPTY (CreatePseudoConsole) | Modern Windows PTY API, supports all shells |
| VT Parser | Custom 13-state machine | Based on Paul Flo Williams model (same as Alacritty, WezTerm) |
| State Management | MVVM with CommunityToolkit.Mvvm | Clean separation, reactive bindings |

### Terminal engine

The VT parser implements the full state machine with 13 states (Ground, Escape, CsiEntry, CsiParam, OscString, etc.) processing byte-at-a-time from the ConPTY output pipe. It handles:

- CSI sequences: cursor movement, erase, insert/delete, scroll regions, mode switching
- SGR: 16-color, 256-color (38;5;n), truecolor (38;2;r;g;b)
- OSC: title setting (0/2), CWD detection (7), notifications (9/99/777)
- ESC: save/restore cursor, alternate screen buffer, index/reverse index
- UTF-8 decoding with proper multi-byte handling

## Keyboard shortcuts

| Shortcut | Action |
|----------|--------|
| Type normally | Input goes to active terminal |
| Ctrl+C | Send interrupt |
| Ctrl+L | Clear screen |
| Arrow keys | Navigate / history |
| Tab | Autocomplete |
| F1-F12 | Function keys passed through |

## Roadmap

cshell is in early development. Here's what's planned:

- [x] ConPTY integration (PowerShell, CMD, WSL)
- [x] VT100/xterm parser with color support
- [x] GPU-accelerated terminal rendering (Win2D)
- [x] Workspace sidebar with shell switching
- [x] Dark theme with Mica backdrop
- [ ] Split panes (horizontal + vertical)
- [ ] Tab bar per pane
- [ ] Notification system (OSC 9/99/777 detection)
- [ ] Git branch display in sidebar
- [ ] Session persistence (save/restore on restart)
- [ ] Configuration file (fonts, colors, shell paths)
- [ ] CLI tool (`cshell.exe`) with named pipe IPC
- [ ] Shell integration scripts (PowerShell, bash, zsh)
- [ ] In-app browser panel (WebView2)
- [ ] Clipboard support (copy/paste selection)
- [ ] Mouse support (selection, scroll)
- [ ] Search in scrollback

## Contributing

Contributions are welcome! See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## License

[MIT](LICENSE) — Shourya Maan Jain
