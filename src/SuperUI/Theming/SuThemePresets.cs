using System.Drawing;

namespace SuperUI.Theming;

/// <summary>
/// 内置主题预设：浅色 / 深色 / 海洋 / 葡萄 / 森林。
/// 预设实例被视为只读，<see cref="SuThemeManager.Apply(SuTheme)"/> 会先克隆再使用，
/// 因此外部修改返回值不会污染预设。
/// </summary>
public static class SuThemePresets
{
    private static readonly SuTheme LightTheme = BuildLight();
    private static readonly SuTheme DarkTheme = BuildDark();
    private static readonly SuTheme OceanTheme = BuildOcean();
    private static readonly SuTheme GrapeTheme = BuildGrape();
    private static readonly SuTheme ForestTheme = BuildForest();

    public static SuTheme Light => LightTheme;

    public static SuTheme Dark => DarkTheme;

    public static SuTheme Ocean => OceanTheme;

    public static SuTheme Grape => GrapeTheme;

    public static SuTheme Forest => ForestTheme;

    /// <summary>全部内置预设。</summary>
    public static IReadOnlyList<SuTheme> All { get; } = new[] { LightTheme, DarkTheme, OceanTheme, GrapeTheme, ForestTheme };

    /// <summary>按名称查找预设（忽略大小写）。</summary>
    public static SuTheme? Find(string name)
        => All.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

    private static Color Hex(string value) => ColorTranslator.FromHtml(value);

    private static SuTheme BuildLight() => new()
    {
        Name = "Light",
        IsDark = false,
        Background = Hex("#F4F5F7"),
        Surface = Hex("#FFFFFF"),
        SurfaceAlt = Hex("#EDF0F4"),
        Border = Hex("#DCDFE6"),
        BorderStrong = Hex("#C4CAD4"),
        TextPrimary = Hex("#1F2329"),
        TextSecondary = Hex("#6B7280"),
        TextPlaceholder = Hex("#A2A9B4"),
        TextDisabled = Hex("#B0B6C0"),
        TextOnAccent = Hex("#FFFFFF"),
        Primary = Hex("#2563EB"),
        PrimaryHover = Hex("#3B82F6"),
        PrimaryPressed = Hex("#1D4ED8"),
        PrimaryDisabled = Hex("#A9C3F0"),
        PrimarySoft = Hex("#E3ECFF"),
        Danger = Hex("#DC2626"),
        DangerHover = Hex("#EF4444"),
        DangerPressed = Hex("#B91C1C"),
        Warning = Hex("#D97706"),
        FocusRing = Hex("#7FA9F5"),
        TitleBarBackground = Hex("#FFFFFF"),
        TitleBarText = Hex("#1F2329"),
        CaptionHover = Hex("#E9ECF1"),
        CaptionCloseHover = Hex("#E81123"),
        Radius = 6,
        FontFamily = "Microsoft YaHei UI",
        FontSize = 9f,
        TitleBarHeight = 44,
    };

    private static SuTheme BuildDark() => new()
    {
        Name = "Dark",
        IsDark = true,
        Background = Hex("#14161B"),
        Surface = Hex("#1A1D24"),
        SurfaceAlt = Hex("#23272F"),
        Border = Hex("#2C3139"),
        BorderStrong = Hex("#3B424D"),
        TextPrimary = Hex("#E7EAF0"),
        TextSecondary = Hex("#98A1AF"),
        TextPlaceholder = Hex("#6F7887"),
        TextDisabled = Hex("#5A616D"),
        TextOnAccent = Hex("#FFFFFF"),
        Primary = Hex("#3B82F6"),
        PrimaryHover = Hex("#5C9BF8"),
        PrimaryPressed = Hex("#2563EB"),
        PrimaryDisabled = Hex("#2C4A7A"),
        PrimarySoft = Hex("#1E2A40"),
        Danger = Hex("#EF4444"),
        DangerHover = Hex("#F87171"),
        DangerPressed = Hex("#DC2626"),
        Warning = Hex("#F59E0B"),
        FocusRing = Hex("#5E9BFF"),
        TitleBarBackground = Hex("#1A1D24"),
        TitleBarText = Hex("#E7EAF0"),
        CaptionHover = Hex("#272C35"),
        CaptionCloseHover = Hex("#E81123"),
        Radius = 6,
        FontFamily = "Microsoft YaHei UI",
        FontSize = 9f,
        TitleBarHeight = 44,
    };

    private static SuTheme BuildOcean()
    {
        var t = BuildLight();
        t.Name = "Ocean";
        t.Background = Hex("#F1F6F6");
        t.SurfaceAlt = Hex("#E7F0F0");
        t.Border = Hex("#D5E3E2");
        t.BorderStrong = Hex("#BCD2D0");
        t.Primary = Hex("#0D9488");
        t.PrimaryHover = Hex("#14B8A6");
        t.PrimaryPressed = Hex("#0F766E");
        t.PrimaryDisabled = Hex("#93D6CC");
        t.PrimarySoft = Hex("#D9F2EE");
        t.FocusRing = Hex("#5AC8BA");
        return t;
    }

    private static SuTheme BuildGrape()
    {
        var t = BuildDark();
        t.Name = "Grape";
        t.Background = Hex("#16121F");
        t.Surface = Hex("#1E1929");
        t.SurfaceAlt = Hex("#272134");
        t.Border = Hex("#322A45");
        t.BorderStrong = Hex("#443A5E");
        t.TextPrimary = Hex("#EDE8F7");
        t.TextSecondary = Hex("#A79DC0");
        t.TextPlaceholder = Hex("#7A7093");
        t.TextDisabled = Hex("#645C79");
        t.Primary = Hex("#A855F7");
        t.PrimaryHover = Hex("#BC7BFB");
        t.PrimaryPressed = Hex("#8B3EE0");
        t.PrimaryDisabled = Hex("#4B3470");
        t.PrimarySoft = Hex("#2A1F3D");
        t.FocusRing = Hex("#C08BFF");
        t.TitleBarBackground = Hex("#1E1929");
        t.TitleBarText = Hex("#EDE8F7");
        t.CaptionHover = Hex("#2C2439");
        return t;
    }

    private static SuTheme BuildForest()
    {
        var t = BuildLight();
        t.Name = "Forest";
        t.Background = Hex("#F3F8F4");
        t.SurfaceAlt = Hex("#E8F1E9");
        t.Border = Hex("#D6E3D8");
        t.BorderStrong = Hex("#BED3C2");
        t.Primary = Hex("#16A34A");
        t.PrimaryHover = Hex("#22C55E");
        t.PrimaryPressed = Hex("#15803D");
        t.PrimaryDisabled = Hex("#A5D9B6");
        t.PrimarySoft = Hex("#DCF3E3");
        t.FocusRing = Hex("#5FD08A");
        return t;
    }
}
