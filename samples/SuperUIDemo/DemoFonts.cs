using System.Drawing;
using SuperUI.Theming;

namespace SuperUIDemo;

/// <summary>演示用的字体缓存，避免主题反复切换时产生字体句柄泄漏。</summary>
internal static class DemoFonts
{
    private static readonly Dictionary<(string Family, float Size, FontStyle Style), Font> Cache = new();

    public static Font Get(SuTheme theme, float size, FontStyle style = FontStyle.Regular)
    {
        var key = (theme.FontFamily, size, style);
        if (Cache.TryGetValue(key, out var font))
        {
            return font;
        }

        try
        {
            font = new Font(theme.FontFamily, size, style, GraphicsUnit.Point);
        }
        catch (ArgumentException)
        {
            font = new Font(FontFamily.GenericSansSerif, size, style, GraphicsUnit.Point);
        }

        Cache[key] = font;
        return font;
    }
}
