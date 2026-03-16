<h1 align="center">wmux</h1>
<p align="center">A native Windows terminal multiplexer with vertical tabs and workspaces — cmux for Windows</p>

<p align="center">
  <img src="https://img.shields.io/badge/platform-Windows%2010%2B-blue" alt="Windows 10+" />
  <img src="https://img.shields.io/badge/.NET-8.0-purple" alt=".NET 8" />
  <img src="https://img.shields.io/badge/UI-WinUI%203-green" alt="WinUI 3" />
  <img src="https://img.shields.io/github/license/shouryamaanjain/wmux" alt="MIT License" />
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

## What is wmux?

wmux is a native Windows terminal multiplexer inspired by [cmux](https://github.com/manaflow-ai/cmux). It gives you vertical workspaces, split panes, and a dark minimal UI — built natively with WinUI 3 and C#.

Auto-detects your shell (PowerShell 7 if installed, otherwise Windows PowerShell). Run CMD or WSL by typing `cmd` or `wsl` in any terminal.

## Features

- **Vertical sidebar** — workspace list with titles, working directory, and unread indicators
- **Auto-detect shell** — prefers `pwsh.exe` (PowerShell 7), falls back to `powershell.exe`
- **GPU-accelerated rendering** — terminal output via Win2D / Direct2D
- **Full VT100/xterm** — 16/256/truecolor, bold, italic, underline, alternate screen buffer
- **Native Windows app** — WinUI 3, Mica backdrop, custom dark theme
- **ConPTY** — modern Windows Pseudo Console API
- **Workspace management** — create, switch, and manage independent terminals

## Install

### Download

Download `Wmux.App.exe` from the [Releases](https://github.com/shouryamaanjain/wmux/releases) page and run it. No installation required.

> **Requirements:** Windows 10 (1903+) or Windows 11. [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) required.

### Build from source

```bash
git clone https://github.com/shouryamaanjain/wmux.git
cd wmux
dotnet build src/Wmux.App/Wmux.App.csproj -c Release -p:Platform=x64
```

## Architecture

```
wmux/
├── src/
│   ├── Wmux.Core/          # Terminal engine (no UI dependencies)
│   │   ├── ConPty/          # Windows Pseudo Console P/Invoke
│   │   ├── VtParser/        # VT100/xterm state machine (13 states)
│   │   ├── Buffer/          # Cell grid, scrollback, cursor, selection
│   │   ├── Terminal/        # Session, emulator, input encoding
│   │   └── Models/          # Workspace, Panel, SplitNode, Notification
│   ├── Wmux.Services/       # Workspace management
│   ├── Wmux.App/            # WinUI 3 application
│   │   ├── Controls/        # TerminalCanvas (Win2D GPU renderer)
│   │   └── Themes/          # Dark theme
│   └── Wmux.Cli/            # CLI tool (planned)
└── wmux.slnx
```

## Roadmap

- [x] ConPTY integration (PowerShell, CMD, WSL)
- [x] VT100/xterm parser with color support
- [x] GPU-accelerated terminal rendering
- [x] Workspace sidebar
- [x] Auto-detect default shell
- [x] Dark theme with Mica backdrop
- [ ] Split panes (horizontal + vertical)
- [ ] Notification system (OSC 9/99/777)
- [ ] Named pipe control server + CLI (`wmux.exe`)
- [ ] Session persistence
- [ ] tmux-compatible command subset
- [ ] Git branch display in sidebar
- [ ] Mouse selection + clipboard
- [ ] Search in scrollback
- [ ] Configuration file
- [ ] Browser panes (WebView2)
- [ ] MSIX packaging

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[MIT](LICENSE) — Shouryamaan Jain
