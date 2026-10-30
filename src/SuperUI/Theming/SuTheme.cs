using System.Drawing;

namespace SuperUI.Theming;

/// <summary>
/// SuperUI 全局主题：一套主题同时决定窗口背景、控件表面、文字、主色、圆角与字体，
/// 所有 SuperUI 控件都只从主题取色，不硬编码颜色，因此可以随时整站换肤。
/// </summary>
public sealed class SuTheme
{
    /// <summary>主题名称（用于 <see cref="SuThemeManager.Apply(string)"/> 与界面展示）。</summary>
    public string Name { get; set; } = "Custom";

    /// <summary>是否为深色主题（影响部分控件的高亮推导）。</summary>
    public bool IsDark { get; set; }

    // ---------- 背景与表面 ----------

    /// <summary>窗口底色。</summary>
    public Color Background { get; set; } = ColorTranslator.FromHtml("#F4F5F7");

    /// <summary>控件表面色（按钮、输入框填充）。</summary>
    public Color Surface { get; set; } = ColorTranslator.FromHtml("#FFFFFF");

    /// <summary>次级表面色（悬停、只读、弱填充）。</summary>
    public Color SurfaceAlt { get; set; } = ColorTranslator.FromHtml("#EDF0F4");

    /// <summary>普通描边色。</summary>
    public Color Border { get; set; } = ColorTranslator.FromHtml("#DCDFE6");

    /// <summary>强调描边色（悬停、窗口外框）。</summary>
    public Color BorderStrong { get; set; } = ColorTranslator.FromHtml("#C4CAD4");

    // ---------- 文字 ----------

    /// <summary>主文字色。</summary>
    public Color TextPrimary { get; set; } = ColorTranslator.FromHtml("#1F2329");

    /// <summary>次要文字色。</summary>
    public Color TextSecondary { get; set; } = ColorTranslator.FromHtml("#6B7280");

    /// <summary>占位符文字色。</summary>
    public Color TextPlaceholder { get; set; } = ColorTranslator.FromHtml("#A2A9B4");

    /// <summary>禁用文字色。</summary>
    public Color TextDisabled { get; set; } = ColorTranslator.FromHtml("#B0B6C0");

    /// <summary>主色上的文字色（一般为白色）。</summary>
    public Color TextOnAccent { get; set; } = ColorTranslator.FromHtml("#FFFFFF");

    // ---------- 主色 ----------

    /// <summary>主色（Primary 按钮、聚焦描边、链接）。</summary>
    public Color Primary { get; set; } = ColorTranslator.FromHtml("#2563EB");

    /// <summary>主色悬停。</summary>
    public Color PrimaryHover { get; set; } = ColorTranslator.FromHtml("#3B82F6");

    /// <summary>主色按下。</summary>
    public Color PrimaryPressed { get; set; } = ColorTranslator.FromHtml("#1D4ED8");

    /// <summary>主色禁用。</summary>
    public Color PrimaryDisabled { get; set; } = ColorTranslator.FromHtml("#A9C3F0");

    /// <summary>主色弱背景（选中态、标签底色）。</summary>
    public Color PrimarySoft { get; set; } = ColorTranslator.FromHtml("#E3ECFF");

    /// <summary>危险色。</summary>
    public Color Danger { get; set; } = ColorTranslator.FromHtml("#DC2626");

    /// <summary>危险色悬停。</summary>
    public Color DangerHover { get; set; } = ColorTranslator.FromHtml("#EF4444");

    /// <summary>危险色按下。</summary>
    public Color DangerPressed { get; set; } = ColorTranslator.FromHtml("#B91C1C");

    /// <summary>警告色（琥珀），用于警告类提示图标 / 徽标，深色主题下会更亮。</summary>
    public Color Warning { get; set; } = ColorTranslator.FromHtml("#D97706");

    /// <summary>聚焦光环色。</summary>
    public Color FocusRing { get; set; } = ColorTranslator.FromHtml("#7FA9F5");

    // ---------- 无边框窗口标题栏 ----------

    /// <summary>标题栏底色。</summary>
    public Color TitleBarBackground { get; set; } = ColorTranslator.FromHtml("#FFFFFF");

    /// <summary>标题栏文字色。</summary>
    public Color TitleBarText { get; set; } = ColorTranslator.FromHtml("#1F2329");

    /// <summary>最小化/最大化按钮悬停底色。</summary>
    public Color CaptionHover { get; set; } = ColorTranslator.FromHtml("#E9ECF1");

    /// <summary>关闭按钮悬停底色。</summary>
    public Color CaptionCloseHover { get; set; } = ColorTranslator.FromHtml("#E81123");

    // ---------- 度量 ----------

    /// <summary>默认圆角半径（像素）。控件可用 Radius 单独覆盖。</summary>
    public int Radius { get; set; } = 6;

    /// <summary>字体族。</summary>
    public string FontFamily { get; set; } = "Microsoft YaHei UI";

    /// <summary>默认字号（pt）。</summary>
    public float FontSize { get; set; } = 9f;

    /// <summary>无边框窗口标题栏高度。</summary>
    public int TitleBarHeight { get; set; } = 44;

    /// <summary>按主题字体创建一个新的 Font 实例（调用方负责释放）。</summary>
    public Font CreateFont(FontStyle style = FontStyle.Regular, float? size = null)
        => new(FontFamily, size ?? FontSize, style, GraphicsUnit.Point);

    /// <summary>浅拷贝，便于基于某个预设快速派生自定义主题。</summary>
    public SuTheme Clone() => (SuTheme)MemberwiseClone();
}
