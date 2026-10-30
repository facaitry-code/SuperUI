using System.Drawing;
using SuperUI.Theming;

namespace SuperUIDemo;

/// <summary>
/// 演示页基类：一个演示就是一块画布，由 <see cref="DemoShellForm"/> 放进内容区，
/// 通过左侧菜单切换；页面自己负责跟随全局主题刷新颜色与字体。
/// </summary>
public abstract class DemoPage : UserControl
{
    protected DemoPage()
    {
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.ResizeRedraw,
            true);

        DoubleBuffered = true;
        AutoScroll = Scrollable;
        Dock = DockStyle.Fill;
        BackColor = SuThemeManager.Current.Background;

        SuThemeManager.ThemeChanged += HandleThemeChanged;
    }

    /// <summary>侧边菜单与标题栏上显示的页面名。</summary>
    public abstract string PageTitle { get; }

    /// <summary>
    /// 内容比内容区大时是否出现滚动条。表格、内嵌菜单这类自己会自适应的页面关掉更干净。
    /// </summary>
    protected virtual bool Scrollable => true;

    /// <summary>当前主题。</summary>
    protected SuTheme Theme => SuThemeManager.Current;

    /// <summary>
    /// 内容排布原点。<see cref="Scrollable"/> 为 true 时内容按显示矩形排布，
    /// 滚动条出现后坐标系依然从内容左上角算起（值为 0,0），和原来的窗体排布代码一致。
    /// </summary>
    protected Point Origin => DisplayRectangle.Location;

    /// <summary>主题切换后刷新页面内的普通 WinForms 控件（Label 之类）。</summary>
    protected virtual void OnPageThemeChanged()
    {
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SuThemeManager.ThemeChanged -= HandleThemeChanged;
        }

        base.Dispose(disposing);
    }

    private void HandleThemeChanged(object? sender, EventArgs e)
    {
        BackColor = Theme.Background;
        OnPageThemeChanged();
        Invalidate(true);
    }
}
