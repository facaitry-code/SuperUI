using System.Drawing;
using SuperUI.Controls;
using SuperUI.Forms;
using SuperUI.Theming;

namespace SuperUIDemo;

/// <summary>消息弹窗页：Info / Warning / Error 三种语义，以及确认框、异常弹窗与长文本换行。</summary>
public sealed class MessageBoxPage : DemoPage
{
    private const int PadX = 28;

    private readonly List<(Label Label, float Size, FontStyle Style, bool Secondary)> _styled = new();
    private readonly Label _status = new();

    public MessageBoxPage()
    {
        BuildUi();
        OnPageThemeChanged();
    }

    public override string PageTitle => "消息弹窗 SuMessageBox";

    private void BuildUi()
    {
        AddHeading("消息弹窗 SuMessageBox", 18, 17f);

        AddCaption(
            "无标题栏圆角卡片 · 语义图标 · 整窗可拖动 · 换主题即时刷新 —— Info / Warning / Error 三种语义",
            PadX,
            56);

        // ---------------- 三种语义 ----------------
        AddHeading("三种语义", 100, 11.5f);

        var info = AddButton("信息 Info", SuButtonVariant.Primary, PadX, 132, 124);
        info.Click += (_, __) => Report(SuMessageBox.Info(Owner(), "已保存 12 条记录，数据校验通过。"));

        var warning = AddButton("警告 Warning", SuButtonVariant.Secondary, 164, 132, 132);
        warning.Click += (_, __) => Report(SuMessageBox.Warning(
            Owner(),
            "C 盘只剩 210 MB 可用空间，建议先清理临时文件再继续。",
            "磁盘空间不足"));

        var error = AddButton("错误 Error", SuButtonVariant.Danger, 308, 132, 124);
        error.Click += (_, __) => Report(SuMessageBox.Error(
            Owner(),
            "连接数据库失败：超时时间已到（30000 ms）。",
            "保存失败"));

        var longText = AddButton("长文本换行", SuButtonVariant.Secondary, 444, 132, 124);
        longText.Click += (_, __) => Report(SuMessageBox.Info(
            Owner(),
            "这段正文会在 384px 处自动换行：SuperUI 的消息弹窗按正文实际宽高量出窗口尺寸，"
            + "标题与正文共用一个文字列，按钮行贴右下角。文字更短时窗口就跟着变窄，"
            + "不会出现一块空荡荡的白板。",
            "自动排版"));

        var exception = AddButton("异常对象", SuButtonVariant.Secondary, 580, 132, 116);
        exception.Click += (_, __) => Report(SuMessageBox.Error(
            new InvalidOperationException("对象当前状态不允许该操作。"),
            Owner()));

        // ---------------- 按钮组合 ----------------
        AddHeading("按钮组合", 200, 11.5f);

        var okCancel = AddButton("确定 / 取消", SuButtonVariant.Secondary, PadX, 232, 124);
        okCancel.Click += (_, __) => Report(SuMessageBox.Show(
            "此操作会覆盖 3 个已存在的文件，是否继续？",
            SuMessageBoxKind.Warning,
            "覆盖确认",
            SuMessageBoxButtons.OkCancel,
            Owner()));

        var yesNo = AddButton("是 / 否", SuButtonVariant.Secondary, 164, 232, 108);
        yesNo.Click += (_, __) =>
        {
            var deleted = SuMessageBox.Confirm(Owner(), "确定要删除选中的 3 行吗？", "删除确认");
            Report(deleted ? DialogResult.Yes : DialogResult.No);
        };

        // ---------------- 遮罩开关 ----------------
        var noScrim = AddButton("关掉遮罩", SuButtonVariant.Secondary, 284, 232, 108);
        noScrim.Click += (_, __) => Report(SuMessageBox.Show(
            "这次 scrim: false —— 宿主窗口保持原样，需要看清背后的内容时就这么用。",
            SuMessageBoxKind.Info,
            "无遮罩",
            SuMessageBoxButtons.Ok,
            Owner(),
            scrim: false));

        var lightScrim = AddButton("淡遮罩 0.15", SuButtonVariant.Secondary, 404, 232, 124);
        lightScrim.Click += (_, __) =>
        {
            using var box = new SuMessageBoxDialog
            {
                Kind = SuMessageBoxKind.Info,
                Text = "淡遮罩",
                Message = "ScrimOpacity = 0.15：背后内容几乎还看得清，只做一点点区分。",
                ScrimHost = Owner() as Control,
                ScrimOpacity = 0.15f,
            };
            Report(ShowBox(box));
        };

        AddCaption("Esc 关闭 · Enter 触发默认按钮 · 点卡片任意空白处可拖动窗口", PadX, 284);

        // ---------------- 上次结果 ----------------
        _status.AutoSize = true;
        _status.UseMnemonic = false;
        _status.Location = ContentLocation(PadX, 322);
        Controls.Add(_status);

        AddCaption("· 默认在父窗口上盖一层整窗压暗的遮罩（ScrimOpacity 可调），没有 owner 时自动不显示", PadX, 356);
        AddCaption("· 没有标题栏：点正文区域即可拖动窗口，双击不会最大化", PadX, 382);
        AddCaption("· 尺寸按内容量出来：短文案是一张小卡片，长文案自动折行并把窗口撑高", PadX, 408);
        AddCaption("· 图标配色取主题的 Primary / Warning / Danger，换肤后连弹窗一起变", PadX, 434);
    }

    /// <summary>把弹窗返回值写到页面上，方便确认 DialogResult 正确。</summary>
    private void Report(DialogResult result)
    {
        _status.Text = $"上一次弹窗返回值：{result}";
    }

    private DialogResult ShowBox(SuMessageBoxDialog box)
    {
        try
        {
            var owner = FindForm();
            return owner is null ? box.ShowDialog() : box.ShowDialog(owner);
        }
        finally
        {
            box.Dispose();
        }
    }

    private IWin32Window? Owner() => FindForm();

    private Point ContentLocation(int x, int y)
    {
        var origin = Origin;
        return new Point(origin.X + x, origin.Y + y);
    }

    private Rectangle ContentBounds(int x, int y, int width, int height)
    {
        var origin = Origin;
        return new Rectangle(origin.X + x, origin.Y + y, width, height);
    }

    private Label AddHeading(string text, int y, float size)
    {
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            Location = ContentLocation(PadX, y),
            UseMnemonic = false,
        };

        Controls.Add(label);
        _styled.Add((label, size, FontStyle.Bold, false));
        return label;
    }

    private Label AddCaption(string text, int x, int y)
    {
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            Location = ContentLocation(x, y),
            UseMnemonic = false,
        };

        Controls.Add(label);
        _styled.Add((label, 9f, FontStyle.Regular, true));
        return label;
    }

    private SuButton AddButton(string text, SuButtonVariant variant, int x, int y, int width, int height = 36)
    {
        var button = new SuButton
        {
            Text = text,
            Variant = variant,
            Bounds = ContentBounds(x, y, width, height),
        };

        Controls.Add(button);
        return button;
    }

    protected override void OnPageThemeChanged()
    {
        var theme = SuThemeManager.Current;

        foreach (var (label, size, style, secondary) in _styled)
        {
            label.Font = DemoFonts.Get(theme, size, style);
            label.ForeColor = secondary ? theme.TextSecondary : theme.TextPrimary;
        }

        _status.Font = DemoFonts.Get(theme, 9f);
        _status.ForeColor = theme.TextSecondary;
        if (string.IsNullOrEmpty(_status.Text))
        {
            _status.Text = "点上面的按钮看看弹窗 —— 每次都把 DialogResult 记在这里。";
        }
    }
}
