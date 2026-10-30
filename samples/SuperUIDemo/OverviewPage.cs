using System.Drawing;
using SuperUI.Controls;
using SuperUI.Theming;

namespace SuperUIDemo;

/// <summary>控件总览页：按钮、输入框、数值、开关、全局主题。</summary>
public sealed class OverviewPage : DemoPage
{
    private const int PadX = 28;

    private readonly List<(Label Label, float Size, FontStyle Style, bool Secondary)> _styled = new();
    private readonly List<(string Key, SuButton Button)> _presets = new();
    private readonly Label _status = new();
    private readonly Action<string> _navigate;

    public OverviewPage(Action<string> navigate)
    {
        _navigate = navigate;

        BuildUi();
        OnPageThemeChanged();
    }

    public override string PageTitle => "控件总览";

    private void BuildUi()
    {
        AddHeading("控件总览", 18, 17f);
        AddCaption("按钮 · 输入框 · 全局主题 · 无边框窗口 —— 全部由一处 SuTheme 统一驱动", PadX, 56);

        // ---------------- 按钮 ----------------
        AddHeading("按钮 SuButton", 96, 11.5f);

        AddButton("主要按钮", SuButtonVariant.Primary, PadX, 128, 128);
        AddButton("次要按钮", SuButtonVariant.Secondary, 172, 128, 128);
        AddButton("幽灵按钮", SuButtonVariant.Ghost, 316, 128, 112);
        AddButton("危险按钮", SuButtonVariant.Danger, 444, 128, 112);
        AddButton("链接按钮", SuButtonVariant.Link, 572, 128, 96);
        AddButton("禁用按钮", SuButtonVariant.Primary, 684, 128, 128).Enabled = false;

        AddButton("小按钮", SuButtonVariant.Secondary, PadX, 180, 96, 30);
        AddButton("默认按钮", SuButtonVariant.Secondary, 140, 178, 116, 36);
        AddButton("大按钮", SuButtonVariant.Primary, 272, 172, 128, 46);

        // 这四个按钮直接切到对应的演示页，跟左侧菜单等价。
        var openDialogButton = AddButton("无边框对话框", SuButtonVariant.Secondary, 420, 178, 152, 36);
        openDialogButton.Click += (_, __) => ShowDemoDialog();

        var galleryButton = AddButton("更多控件", SuButtonVariant.Secondary, 584, 178, 96, 36);
        galleryButton.Click += (_, __) => _navigate("gallery");

        var menuButton = AddButton("侧面菜单", SuButtonVariant.Secondary, 688, 178, 96, 36);
        menuButton.Click += (_, __) => _navigate("menu");

        var tableButton = AddButton("表格", SuButtonVariant.Secondary, 792, 178, 88, 36);
        tableButton.Click += (_, __) => _navigate("table");

        // ---------------- 输入框 ----------------
        AddHeading("输入框 SuTextBox", 240, 11.5f);

        AddInput("请输入用户名", PadX, 272, 260, 40);
        var password = AddInput("请输入密码", 304, 272, 260, 40);
        password.UseSystemPasswordChar = true;

        var readOnlyInput = AddInput(string.Empty, 580, 272, 340, 40);
        readOnlyInput.ReadOnly = true;
        readOnlyInput.Text = "只读内容 · ReadOnly = true";

        var clearable = AddInput("带清除按钮", PadX, 324, 260, 40);
        clearable.ShowClearButton = true;
        clearable.Text = "点右侧 ✕ 清空";

        var disabledInput = AddInput("禁用状态", 304, 324, 260, 40);
        disabledInput.Text = "不可编辑";
        disabledInput.Enabled = false;

        AddCaption("聚焦时描边变主色，悬停变强描边", 580, 336);

        // ---------------- 数值输入 ----------------
        AddNumber("数量", 28, 372, 126, 12m, 1m, 0m, 100m);
        AddNumber("折扣", 210, 372, 126, 8.5m, 0.5m, 0m, 100m, 1, "%");
        AddNumber("金额", 392, 372, 164, 12500.5m, 100m, 0m, 1000000m, 2, string.Empty, true);

        var disabledNumber = AddNumber("禁用", 612, 372, 112, 300m, 1m, 0m, 500m);
        disabledNumber.Enabled = false;

        var readOnlyNumber = AddNumber("只读", 766, 372, 104, 42m, 1m, 0m, 100m);
        readOnlyNumber.ReadOnly = true;

        var multiline = AddInput("多行输入：支持换行、自动纵向滚动条……", PadX, 424, 892, 100);
        multiline.Multiline = true;

        // ---------------- 开关 ----------------
        AddHeading("开关 SuSwitch", 544, 11.5f);

        AddSwitch("启用通知", PadX, 572, true);
        AddSwitch("自动更新", 168, 572, false);
        AddSwitch("禁用状态", 288, 572, true).Enabled = false;
        AddSwitch("只读（不可切换）", 408, 572, false).ReadOnly = true;

        var custom = AddSwitch("自定义颜色", 600, 572, true);
        custom.OnColor = Color.FromArgb(0x16, 0xA3, 0x4A);

        // ---------------- 主题 ----------------
        AddHeading("全局主题 SuTheme", 618, 11.5f);

        var presets = new[]
        {
            ("浅色", "Light"),
            ("深色", "Dark"),
            ("海洋", "Ocean"),
            ("葡萄", "Grape"),
            ("森林", "Forest"),
        };

        var x = PadX;
        foreach (var (caption, key) in presets)
        {
            var presetKey = key;
            var button = AddButton(caption, SuButtonVariant.Secondary, x, 646, 88, 34);
            button.Click += (_, __) => SuThemeManager.Apply(presetKey);
            _presets.Add((presetKey, button));
            x += 98;
        }

        var toggle = AddButton("深浅反转", SuButtonVariant.Ghost, x + 12, 646, 120, 34);
        toggle.Click += (_, __) => SuThemeManager.ToggleLightDark();

        // ---------------- 状态 ----------------
        _status.AutoSize = true;
        _status.Location = ContentLocation(PadX, 700);
        _status.UseMnemonic = false;
        Controls.Add(_status);
    }

    private void ShowDemoDialog()
    {
        using var dialog = new DemoDialog();
        var owner = FindForm();
        if (owner is null)
        {
            dialog.ShowDialog();
            return;
        }

        dialog.ShowDialog(owner);
    }

    /// <summary>左侧说明文字 + 数值输入框。</summary>
    private SuNumericUpDown AddNumber(
        string label,
        int x,
        int y,
        int width,
        decimal value,
        decimal increment,
        decimal minimum,
        decimal maximum,
        int decimals = 0,
        string unit = "",
        bool thousands = false)
    {
        var caption = new Label
        {
            Text = label,
            AutoSize = true,
            UseMnemonic = false,
            Location = ContentLocation(x, y + 11),
        };

        Controls.Add(caption);
        _styled.Add((caption, 9f, FontStyle.Regular, true));

        var input = new SuNumericUpDown
        {
            Minimum = minimum,
            Maximum = maximum,
            Increment = increment,
            DecimalPlaces = decimals,
            ThousandsSeparator = thousands,
            Unit = unit,
            Value = value,
            AutoHeight = false,
            Bounds = ContentBounds(x + 44, y, width, 40),
        };

        Controls.Add(input);
        return input;
    }

    /// <summary>内容坐标 → 页面客户区坐标（页面没有标题栏，所以原点通常是 0,0）。</summary>
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

    private SuTextBox AddInput(string placeholder, int x, int y, int width, int height)
    {
        var input = new SuTextBox
        {
            PlaceholderText = placeholder,
            AutoHeight = false,
            Bounds = ContentBounds(x, y, width, height),
        };

        Controls.Add(input);
        return input;
    }

    private SuSwitch AddSwitch(string text, int x, int y, bool isChecked)
    {
        var origin = Origin;
        var toggle = new SuSwitch
        {
            Text = text,
            Checked = isChecked,
            Location = new Point(origin.X + x, origin.Y + y),
        };

        Controls.Add(toggle);
        return toggle;
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
        _status.Text = $"当前主题：{theme.Name}{(theme.IsDark ? "（深色）" : "（浅色）")} · 圆角 {theme.Radius}px · "
                       + $"字体 {theme.FontFamily} {theme.FontSize}pt · 左侧菜单切换演示页";

        foreach (var (key, button) in _presets)
        {
            button.Variant = string.Equals(key, theme.Name, StringComparison.OrdinalIgnoreCase)
                ? SuButtonVariant.Primary
                : SuButtonVariant.Secondary;
        }
    }
}
