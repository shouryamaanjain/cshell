using System.Text;

namespace CShell.Core.Terminal;

public static class TerminalInput
{
    public static byte[] EncodeKey(
        string key,
        bool shift = false,
        bool ctrl = false,
        bool alt = false,
        bool applicationCursorKeys = false)
    {
        // Special keys
        var seq = key switch
        {
            "Enter" => "\r"u8.ToArray(),
            "Tab" => shift ? "\x1b[Z"u8.ToArray() : "\t"u8.ToArray(),
            "Escape" => "\x1b"u8.ToArray(),
            "Backspace" => ctrl ? new byte[] { 0x08 } : new byte[] { 0x7F },
            "Delete" => "\x1b[3~"u8.ToArray(),
            "Insert" => "\x1b[2~"u8.ToArray(),
            "Home" => applicationCursorKeys ? "\x1bOH"u8.ToArray() : "\x1b[H"u8.ToArray(),
            "End" => applicationCursorKeys ? "\x1bOF"u8.ToArray() : "\x1b[F"u8.ToArray(),
            "PageUp" => "\x1b[5~"u8.ToArray(),
            "PageDown" => "\x1b[6~"u8.ToArray(),
            "Up" => applicationCursorKeys ? "\x1bOA"u8.ToArray() : "\x1b[A"u8.ToArray(),
            "Down" => applicationCursorKeys ? "\x1bOB"u8.ToArray() : "\x1b[B"u8.ToArray(),
            "Right" => applicationCursorKeys ? "\x1bOC"u8.ToArray() : "\x1b[C"u8.ToArray(),
            "Left" => applicationCursorKeys ? "\x1bOD"u8.ToArray() : "\x1b[D"u8.ToArray(),
            "F1" => "\x1bOP"u8.ToArray(),
            "F2" => "\x1bOQ"u8.ToArray(),
            "F3" => "\x1bOR"u8.ToArray(),
            "F4" => "\x1bOS"u8.ToArray(),
            "F5" => "\x1b[15~"u8.ToArray(),
            "F6" => "\x1b[17~"u8.ToArray(),
            "F7" => "\x1b[18~"u8.ToArray(),
            "F8" => "\x1b[19~"u8.ToArray(),
            "F9" => "\x1b[20~"u8.ToArray(),
            "F10" => "\x1b[21~"u8.ToArray(),
            "F11" => "\x1b[23~"u8.ToArray(),
            "F12" => "\x1b[24~"u8.ToArray(),
            _ => null
        };

        if (seq != null)
        {
            if (alt) return [0x1b, .. seq];
            return seq;
        }

        // Single character
        if (key.Length == 1)
        {
            char ch = key[0];

            if (ctrl && ch >= 'a' && ch <= 'z')
                return [(byte)(ch - 'a' + 1)];
            if (ctrl && ch >= 'A' && ch <= 'Z')
                return [(byte)(ch - 'A' + 1)];
            if (ctrl && ch == ' ')
                return [0x00];
            if (ctrl && ch == '\\')
                return [0x1C];
            if (ctrl && ch == ']')
                return [0x1D];

            var bytes = Encoding.UTF8.GetBytes(key);
            if (alt) return [0x1b, .. bytes];
            return bytes;
        }

        return Encoding.UTF8.GetBytes(key);
    }

    public static byte[] EncodePaste(string text, bool bracketedPasteMode)
    {
        var payload = Encoding.UTF8.GetBytes(text);
        if (!bracketedPasteMode) return payload;

        var result = new byte[payload.Length + 12]; // ESC[200~ ... ESC[201~
        "\x1b[200~"u8.CopyTo(result);
        payload.CopyTo(result.AsSpan(6));
        "\x1b[201~"u8.CopyTo(result.AsSpan(6 + payload.Length));
        return result;
    }
}
