using System.Drawing;
using SuperUI.Controls;
using SuperUI.Forms;
using SuperUI.Theming;

namespace SuperUIDemo;

/// <summary>演示无边框对话框：无最小化/最大化按钮、固定尺寸、居中于父窗口。</summary>
public sealed class DemoDialog : SuBorderlessForm
{
    public DemoDialog()
    {
        Text = "无边框对话框 · SuperUI";
        ClientSize = new Size(470, 300);
        StartPosition = FormStartPosition.CenterParent;
        ShowMinimizeBox = false;
        ShowMaximizeBox = false;
        Resizable = false;
        MinimumSize = Size;
        MaximumSize = Size;

        var theme = SuThemeManager.Current;

        // 用 DisplayRectangle 的原点定位，自动避开标题栏与缩放热区。
        var origin = DisplayRectangle;

        var heading = new Label
        {
            Text = "确认操作",
            AutoSize = true,
            Location = new Point(origin.X + 22, origin.Y + 20),
            Font = DemoFonts.Get(theme, 13f, FontStyle.Bold),
            ForeColor = theme.TextPrimary,
            UseMnemonic = false,
        };

        var caption = new Label
        {
            Text = "这是同一个 SuBorderlessForm：圆角、DWM 投影、标题栏按钮可按需隐藏。",
            AutoSize = true,
            Location = new Point(origin.X + 22, origin.Y + 54),
            Font = DemoFonts.Get(theme, 9f),
            ForeColor = theme.TextSecondary,
            UseMnemonic = false,
        };

        var input = new SuTextBox
        {
            PlaceholderText = "输入点什么试试……",
            AutoHeight = false,
            Bounds = new Rectangle(origin.X + 22, origin.Y + 88, 414, 40),
        };

        var cancel = new SuButton
        {
            Text = "取消",
            Variant = SuButtonVariant.Secondary,
            DialogResult = DialogResult.Cancel,
            Bounds = new Rectangle(origin.X + 250, origin.Y + 164, 88, 36),
        };

        var ok = new SuButton
        {
            Text = "确定",
            Variant = SuButtonVariant.Primary,
            DialogResult = DialogResult.OK,
            Bounds = new Rectangle(origin.X + 348, origin.Y + 164, 88, 36),
        };

        Controls.Add(heading);
        Controls.Add(caption);
        Controls.Add(input);
        Controls.Add(cancel);
        Controls.Add(ok);

        AcceptButton = ok;
        CancelButton = cancel;
    }
}
