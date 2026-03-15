using System.Text;

namespace CShell.Core.VtParser;

public sealed class OscParser
{
    public event Action<string>? DirectoryChanged;
    public event Action<string, string>? NotificationReceived;

    public void Parse(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 2) return;

        // Find the OSC number (digits before the first ';')
        int semicolonIdx = payload.IndexOf((byte)';');
        if (semicolonIdx <= 0) return;

        var numberSpan = payload[..semicolonIdx];
        if (!TryParseOscNumber(numberSpan, out int oscNumber)) return;

        var data = payload[(semicolonIdx + 1)..];
        var text = Encoding.UTF8.GetString(data);

        switch (oscNumber)
        {
            case 7:
                ParseOsc7(text);
                break;
            case 9:
                NotificationReceived?.Invoke(text, "");
                break;
            case 99:
                ParseOsc99(text);
                break;
            case 777:
                ParseOsc777(text);
                break;
        }
    }

    private void ParseOsc7(string text)
    {
        // Format: file://hostname/path
        if (text.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = text[7..];
            int slashIdx = uri.IndexOf('/');
            if (slashIdx >= 0)
            {
                var path = Uri.UnescapeDataString(uri[slashIdx..]);
                DirectoryChanged?.Invoke(path);
            }
        }
    }

    private void ParseOsc99(string text)
    {
        // Kitty notification: key=value pairs separated by ';'
        string title = "";
        string body = "";
        foreach (var part in text.Split(';'))
        {
            var eq = part.IndexOf('=');
            if (eq < 0) continue;
            var key = part[..eq];
            var value = part[(eq + 1)..];
            if (key == "t" || key == "title") title = value;
            else if (key == "b" || key == "body") body = value;
        }
        if (title.Length > 0 || body.Length > 0)
            NotificationReceived?.Invoke(title, body);
    }

    private void ParseOsc777(string text)
    {
        // Format: notify;title;body
        var parts = text.Split(';', 3);
        if (parts.Length >= 1 && parts[0] == "notify")
        {
            var title = parts.Length >= 2 ? parts[1] : "";
            var body = parts.Length >= 3 ? parts[2] : "";
            NotificationReceived?.Invoke(title, body);
        }
    }

    private static bool TryParseOscNumber(ReadOnlySpan<byte> span, out int number)
    {
        number = 0;
        foreach (var b in span)
        {
            if (b < (byte)'0' || b > (byte)'9') return false;
            number = number * 10 + (b - '0');
        }
        return true;
    }
}
