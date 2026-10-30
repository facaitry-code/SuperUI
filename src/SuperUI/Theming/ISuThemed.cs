namespace SuperUI.Theming;

/// <summary>
/// 任何需要跟随全局主题刷新的控件/窗口都实现该接口。
/// 通过 <see cref="SuThemeManager.Register"/> 注册后，主题切换时会被自动通知。
/// </summary>
public interface ISuThemed
{
    /// <summary>当前生效的主题（通常直接返回 <see cref="SuThemeManager.Current"/>）。</summary>
    SuTheme Theme { get; }

    /// <summary>主题发生变化时被调用，实现方在这里重新计算颜色/字体/布局并重绘。</summary>
    void OnThemeChanged();
}
