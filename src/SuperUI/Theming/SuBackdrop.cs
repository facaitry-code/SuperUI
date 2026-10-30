using System.Drawing;

namespace SuperUI.Theming;

/// <summary>
/// 解析控件背后的“底色”。SuperUI 控件不使用 WinForms 的透明背景机制
/// （透明控件合成在其他子控件之上时会出现像素串色），而是直接用父容器实色填充。
/// </summary>
internal static class SuBackdrop
{
    public static Color Resolve(Control control, Color? preferred = null)
    {
        if (preferred.HasValue)
        {
            return preferred.Value;
        }

        for (var parent = control.Parent; parent is not null; parent = parent.Parent)
        {
            var color = parent.BackColor;
            if (color.A == 255)
            {
                return color;
            }
        }

        return SuThemeManager.Current.Background;
    }
}
