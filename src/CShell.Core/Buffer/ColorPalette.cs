namespace CShell.Core.Buffer;

public static class ColorPalette
{
    public static readonly uint DefaultForeground = 0xFFDDDDDD;
    public static readonly uint DefaultBackground = 0xFF1E1E1E;

    // Standard 16 ANSI colors (dark variants first, then bright)
    private static readonly uint[] Ansi16 =
    [
        0xFF000000, // 0  Black
        0xFFCD3131, // 1  Red
        0xFF0DBC79, // 2  Green
        0xFFE5E510, // 3  Yellow
        0xFF2472C8, // 4  Blue
        0xFFBC3FBC, // 5  Magenta
        0xFF11A8CD, // 6  Cyan
        0xFFE5E5E5, // 7  White
        0xFF666666, // 8  Bright Black
        0xFFF14C4C, // 9  Bright Red
        0xFF23D18B, // 10 Bright Green
        0xFFF5F543, // 11 Bright Yellow
        0xFF3B8EEA, // 12 Bright Blue
        0xFFD670D6, // 13 Bright Magenta
        0xFF29B8DB, // 14 Bright Cyan
        0xFFFFFFFF, // 15 Bright White
    ];

    // 256-color palette (lazily generated)
    private static uint[]? _palette256;

    public static uint GetAnsiColor(int index)
    {
        if (index >= 0 && index < 16)
            return Ansi16[index];
        return Get256Color(index);
    }

    public static uint Get256Color(int index)
    {
        _palette256 ??= Build256Palette();
        if (index >= 0 && index < 256)
            return _palette256[index];
        return DefaultForeground;
    }

    public static uint FromRgb(int r, int g, int b)
    {
        return 0xFF000000 | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
    }

    private static uint[] Build256Palette()
    {
        var palette = new uint[256];

        // 0-15: Standard ANSI
        Array.Copy(Ansi16, palette, 16);

        // 16-231: 6x6x6 color cube
        for (int i = 0; i < 216; i++)
        {
            int r = (i / 36) * 51;
            int g = ((i / 6) % 6) * 51;
            int b = (i % 6) * 51;
            palette[16 + i] = FromRgb(r, g, b);
        }

        // 232-255: Grayscale ramp
        for (int i = 0; i < 24; i++)
        {
            int v = 8 + i * 10;
            palette[232 + i] = FromRgb(v, v, v);
        }

        return palette;
    }
}
