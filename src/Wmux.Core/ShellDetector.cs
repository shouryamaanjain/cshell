namespace Wmux.Core;

public static class ShellDetector
{
    private static string? _cachedShell;

    /// <summary>
    /// Detects the best available shell, matching Windows Terminal's behavior:
    /// prefer pwsh.exe (PowerShell 7+), fall back to powershell.exe.
    /// </summary>
    public static string GetDefaultShell()
    {
        if (_cachedShell != null) return _cachedShell;

        // Check if PowerShell 7+ (pwsh.exe) is available
        if (FindInPath("pwsh.exe") != null)
        {
            _cachedShell = "pwsh.exe";
            return _cachedShell;
        }

        // Fall back to Windows PowerShell (always available)
        _cachedShell = "powershell.exe";
        return _cachedShell;
    }

    private static string? FindInPath(string exe)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv)) return null;

        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            try
            {
                var fullPath = Path.Combine(dir, exe);
                if (File.Exists(fullPath)) return fullPath;
            }
            catch { }
        }

        return null;
    }
}
