using System.Drawing;
using SuperUI.Controls;
using SuperUI.Theming;

namespace SuperUIDemo;

/// <summary>
/// 侧面菜单页：页面内再放一份 SuSideMenu 实例，演示分组、子项折叠、徽标、禁用项与键盘/滚轮导航。
/// 本演示台左侧的导航用的就是同一个控件。
/// </summary>
public sealed class MenuPage : DemoPage
{
    private readonly SuSideMenu _demoMenu = new();
    private readonly Label _heading = new();
    private readonly Label _caption = new();
    private readonly Label _selectedText = new();
    private readonly SuLabel _selectedKey = new();
    private readonly List<(Label Label, float Size, FontStyle Style, bool Secondary)> _styled = new();
    private SuMenuItem? _defaultItem;

    public MenuPage()
    {
        _demoMenu.Width = 220;
        _demoMenu.Dock = DockStyle.Left;
        Controls.Add(_demoMenu);
        BuildMenu();

        var origin = Origin;
        var left = origin.X + 248;

        ConfigureLabel(_heading, new Point(left, origin.Y + 24), 15f, FontStyle.Bold, false);
        _heading.Text = "侧面菜单 SuSideMenu";

        ConfigureLabel(_caption, new Point(left, origin.Y + 58), 9f, FontStyle.Regular, true);
        _caption.MaximumSize = new Size(640, 0);
        _caption.Text = "左侧这一栏就是本演示台的导航：分组标题 + 折叠子项 + 徽标 + 禁用项 + 滚轮/键盘导航，"
                        + "选中项带主色浅底与左侧强调条。右边再放一份独立实例演示完整菜单结构。";

        ConfigureLabel(_selectedText, new Point(left, origin.Y + 126), 11f, FontStyle.Bold, false);
        _selectedText.Text = "当前选中";

        _selectedKey.AutoSize = true;
        _selectedKey.UseMnemonic = false;
        _selectedKey.Location = new Point(left, origin.Y + 152);
        Controls.Add(_selectedKey);

        var expand = new SuButton
        {
            Text = "全部展开",
            Variant = SuButtonVariant.Secondary,
            Bounds = new Rectangle(left, origin.Y + 192, 108, 36),
        };
        expand.Click += (_, __) => _demoMenu.ExpandAll();
        Controls.Add(expand);

        var collapse = new SuButton
        {
            Text = "全部收起",
            Variant = SuButtonVariant.Secondary,
            Bounds = new Rectangle(left + 120, origin.Y + 192, 108, 36),
        };
        collapse.Click += (_, __) => _demoMenu.CollapseAll();
        Controls.Add(collapse);

        var select = new SuButton
        {
            Text = "定位到「密码输入」",
            Variant = SuButtonVariant.Ghost,
            Bounds = new Rectangle(left + 240, origin.Y + 192, 168, 36),
        };
        select.Click += (_, __) => _demoMenu.SelectByKey("input-password");
        Controls.Add(select);

        var toggle = new SuButton
        {
            Text = "深浅反转",
            Variant = SuButtonVariant.Primary,
            Bounds = new Rectangle(left + 420, origin.Y + 192, 108, 36),
        };
        toggle.Click += (_, __) => SuThemeManager.ToggleLightDark();
        Controls.Add(toggle);

        _demoMenu.SelectedItemChanged += (_, __) => UpdateSelected();
        OnPageThemeChanged();

        if (_defaultItem is not null)
        {
            _demoMenu.SelectedItem = _defaultItem;
        }

        UpdateSelected();
    }

    public override string PageTitle => "侧面菜单";

    /// <summary>页面内的菜单自己撑满高度，不要外层滚动条。</summary>
    protected override bool Scrollable => false;

    private void BuildMenu()
    {
        var overview = new SuMenuItem("控件总览") { Key = "overview", Badge = "new" };
        var button = new SuMenuItem("按钮 SuButton") { Key = "button" };
        var input = new SuMenuItem("输入框 SuTextBox") { Key = "input", Badge = "3" };
        input.Add(new SuMenuItem("单行输入") { Key = "input-single" });
        input.Add(new SuMenuItem("密码输入") { Key = "input-password" });
        input.Add(new SuMenuItem("多行输入") { Key = "input-multiline" });

        var frame = new SuMenuItem("无边框窗口") { Key = "frame" };
        var dialog = new SuMenuItem("无边框对话框") { Key = "dialog" };
        var theme = new SuMenuItem("全局主题") { Key = "theme", Badge = "5" };

        _demoMenu.AddGroup("基础组件");
        _demoMenu.Add(overview);
        _demoMenu.Add(button);
        _demoMenu.Add(input);

        _demoMenu.AddGroup("窗口与主题");
        _demoMenu.Add(frame);
        _demoMenu.Add(dialog);
        _demoMenu.Add(theme);

        _demoMenu.AddSeparator();

        _demoMenu.AddGroup("更多示例");
        _demoMenu.Add(new SuMenuItem("列表与表格") { Key = "list" });
        _demoMenu.Add(new SuMenuItem("树形结构") { Key = "tree" });

        var disabled = new SuMenuItem("消息提示（禁用）") { Key = "toast", Enabled = false };
        _demoMenu.Add(disabled);
        _demoMenu.Add(new SuMenuItem("通知中心") { Key = "notify", Badge = "99+" });
        _demoMenu.Add(new SuMenuItem("空状态") { Key = "empty" });

        overview.IsExpanded = false;
        input.IsExpanded = true;
        _defaultItem = overview;
    }

    private void ConfigureLabel(Label label, Point location, float size, FontStyle style, bool secondary)
    {
        label.AutoSize = true;
        label.Location = location;
        label.UseMnemonic = false;
        _styled.Add((label, size, style, secondary));
        Controls.Add(label);
    }

    private void UpdateSelected()
    {
        var item = _demoMenu.SelectedItem;
        _selectedText.Text = item is null ? "未选择" : $"当前选中：{item.Text}";
        _selectedKey.Text = item is null ? string.Empty : $"SelectedKey = {item.Key}";
    }

    protected override void OnPageThemeChanged()
    {
        var theme = SuThemeManager.Current;

        foreach (var (label, size, style, secondary) in _styled)
        {
            label.Font = DemoFonts.Get(theme, size, style);
            label.ForeColor = secondary ? theme.TextSecondary : theme.TextPrimary;
        }

        _selectedKey.ForeColor = theme.TextSecondary;
    }
}
