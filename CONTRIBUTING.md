# Contributing to wmux

Thanks for your interest in contributing to wmux!

## Prerequisites

- Windows 10 (1903+) or Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Git

## Getting started

1. Fork the repository
2. Clone your fork:
   ```bash
   git clone https://github.com/YOUR-USERNAME/wmux.git
   cd wmux
   ```
3. Build:
   ```bash
   dotnet build src/Wmux.App/Wmux.App.csproj -c Debug -p:Platform=x64
   ```
4. Run:
   ```bash
   dotnet run --project src/Wmux.App/Wmux.App.csproj
   ```

## Project structure

| Project | Description |
|---------|-------------|
| `Wmux.Core` | Terminal engine — ConPTY, VT parser, buffer, models. No UI dependencies. |
| `Wmux.Services` | Application services — workspace management, persistence. |
| `Wmux.App` | WinUI 3 application — window, sidebar, terminal canvas renderer. |
| `Wmux.Cli` | CLI tool (planned) — named pipe client for controlling wmux. |

## Development workflow

1. Create a branch from `main`
2. Make your changes
3. Test by building and running the app
4. Submit a pull request

## Code style

- Use C# conventions (PascalCase for public members, camelCase with `_` prefix for private fields)
- Keep methods focused and short
- Add comments only where the logic isn't self-evident

## Areas where help is needed

- **Split pane system** — horizontal/vertical split with draggable dividers
- **Mouse support** — text selection, scroll, right-click context menu
- **Clipboard** — copy selected text, paste from clipboard
- **Configuration** — JSON config file for fonts, colors, keybindings
- **Performance** — optimize the Win2D rendering loop (dirty-row tracking, text run batching)
- **Testing** — unit tests for VT parser and terminal buffer

## Reporting bugs

Open an issue with:
- What you expected to happen
- What actually happened
- Steps to reproduce
- Your Windows version and .NET SDK version

## License

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE).
