using System.Drawing;
using SuperUI.Controls;
using SuperUI.Forms;
using SuperUI.Theming;

namespace SuperUIDemo;

/// <summary>
/// 演示台主窗口：一个窗口装下所有演示。
/// 左侧 <see cref="SuSideMenu"/> 做导航，右侧内容区按需创建并切换 <see cref="DemoPage"/>，
/// 底部一排主题预设随时换肤——标题栏、菜单、每个页面都跟着同一个 SuTheme 刷新。
/// </summary>
public sealed class DemoShellForm : SuBorderlessForm
{
    private const int SidebarWidth = 236;
    private const int ThemeStripHeight = 88;

    private readonly Dictionary<string, Func<DemoPage>> _factories;
    private readonly Dictionary<string, DemoPage> _pages = new();
    private readonly Panel _content = new();
    private readonly Panel _sidebar = new();
    private readonly Panel _themeStrip = new();
    private readonly SuSideMenu _menu = new();
    private readonly SuLabel _themeLabel = new();
    private readonly SuComboBox _themeCombo = new();
    private readonly SuMenuItem _overviewItem = new("控件总览") { Key = "overview", Badge = "1" };
    private readonly SuMenuItem _galleryItem = new("更多控件") { Key = "gallery" };
    private readonly SuMenuItem _tableItem = new("表格 SuTable") { Key = "table", Badge = "240" };
    private readonly SuMenuItem _paginationItem = new("分页 SuPagination") { Key = "pagination" };
    private readonly SuMenuItem _messageBoxItem = new("消息弹窗 SuMessageBox") { Key = "messagebox" };
    private readonly SuMenuItem _menuItem = new("侧面菜单") { Key = "menu" };

    private readonly bool _showcaseTable;
    private bool _navigating;
    private bool _syncingTheme;

    public DemoShellForm(bool showcaseTable = false, int titleBarHeight = 42)
    {
        _showcaseTable = showcaseTable;

        Text = "SuperUI · 控件演示台";
        ClientSize = new Size(1180, 820);
        MinimumSize = new Size(940, 620);
        TitleBarHeight = titleBarHeight;

        _factories = new Dictionary<string, Func<DemoPage>>(StringComparer.OrdinalIgnoreCase)
        {
            ["overview"] = () => new OverviewPage(NavigateTo),
            ["gallery"] = () => new GalleryPage(),
            ["table"] = () => new TablePage(_showcaseTable),
            ["pagination"] = () => new PaginationPage(),
            ["messagebox"] = () => new MessageBoxPage(),
            ["menu"] = () => new MenuPage(),
        };

        // 内容区先加（Dock=Fill），侧栏后加（Dock=Left），Fill 会自动让出左侧那一列。
        _content.Dock = DockStyle.Fill;
        _content.BackColor = Theme.Background;
        Controls.Add(_content);

        BuildSidebar();
        Controls.Add(_sidebar);

        _menu.SelectedItemChanged += (_, __) =>
        {
            if (_navigating)
            {
                return;
            }

            NavigateTo(_menu.SelectedKey ?? string.Empty);
        };

        SuThemeManager.ThemeChanged += HandleThemeChanged;

        HandleThemeChanged(null, EventArgs.Empty);
        NavigateTo("overview");
    }

    /// <summary>当前显示页面的 Key。</summary>
    public string CurrentKey { get; private set; } = string.Empty;

    /// <summary>页面容器（自检用）。</summary>
    internal Panel ContentHost => _content;

    /// <summary>左侧导航（自检用）。</summary>
    internal SuSideMenu SideMenu => _menu;

    /// <summary>左侧导航所在的那一列（自检用）。</summary>
    internal Panel Sidebar => _sidebar;

    /// <summary>切到某个演示页：第一次进入时创建页面，之后保留状态直接显示。</summary>
    public void NavigateTo(string key)
    {
        if (string.IsNullOrEmpty(key) || !_factories.TryGetValue(key, out var factory))
        {
            return;
        }

        if (!_pages.TryGetValue(key, out var page))
        {
            page = factory();
            _pages.Add(key, page);
            _content.Controls.Add(page);
        }

        foreach (var pair in _pages)
        {
            pair.Value.Visible = ReferenceEquals(pair.Value, page);
        }

        CurrentKey = key;
        Text = "SuperUI · " + page.PageTitle;

        // 从按钮/代码进来时同步菜单选中项；从菜单进来时 key 已经一致，不会重入。
        if (_menu.SelectedKey != key)
        {
            _navigating = true;
            try
            {
                _menu.SelectByKey(key);
            }
            finally
            {
                _navigating = false;
            }
        }
    }

    /// <summary>打开无边框对话框（演示页和启动参数都用它）。</summary>
    public void ShowDemoDialog()
    {
        using var dialog = new DemoDialog();
        dialog.ShowDialog(this);
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

    private void BuildSidebar()
    {
        _sidebar.Dock = DockStyle.Left;
        _sidebar.Width = SidebarWidth;
        _sidebar.BackColor = Theme.Background;

        _menu.Dock = DockStyle.Fill;
        _sidebar.Controls.Add(_menu);

        _themeStrip.Dock = DockStyle.Bottom;
        _themeStrip.Height = ThemeStripHeight;
        _themeStrip.BackColor = Theme.Background;
        _sidebar.Controls.Add(_themeStrip);

        _themeLabel.AutoSize = true;
        _themeLabel.UseMnemonic = false;
        _themeLabel.Text = "全局主题 · 5 套预设";
        _themeLabel.Location = new Point(14, 14);
        _themeStrip.Controls.Add(_themeLabel);

        _themeCombo.Bounds = new Rectangle(12, 42, SidebarWidth - 26, 34);
        _themeCombo.Items.AddRange(new object[] { "浅色 Light", "深色 Dark", "海洋 Ocean", "葡萄 Grape", "森林 Forest" });
        _themeCombo.SelectedIndexChanged += (_, __) =>
        {
            if (_syncingTheme)
            {
                return;
            }

            var index = _themeCombo.SelectedIndex;
            if (index >= 0 && index < ThemeKeys.Length)
            {
                SuThemeManager.Apply(ThemeKeys[index]);
            }
        };
        _themeStrip.Controls.Add(_themeCombo);

        BuildMenu();
    }

    private static readonly string[] ThemeKeys = { "Light", "Dark", "Ocean", "Grape", "Forest" };

    private void BuildMenu()
    {
        _menu.AddGroup("演示页面");
        _menu.Add(_overviewItem);
        _menu.Add(_galleryItem);
        _menu.Add(_tableItem);
        _menu.Add(_paginationItem);
        _menu.Add(_messageBoxItem);
        _menu.Add(_menuItem);

        _menu.AddSeparator();
        _menu.AddGroup("关于");
        _menu.Add(new SuMenuItem("SuperUI · WinForms 自绘控件") { Key = "about", Enabled = false });
        _menu.Add(new SuMenuItem("一个窗口 · 菜单切换") { Key = "hint", Badge = "MVP", Enabled = false });
    }

    private void HandleThemeChanged(object? sender, EventArgs e)
    {
        var theme = SuThemeManager.Current;

        _content.BackColor = theme.Background;
        _sidebar.BackColor = theme.Background;
        _themeStrip.BackColor = theme.Background;
        _themeLabel.ForeColor = theme.TextSecondary;

        _syncingTheme = true;
        try
        {
            var index = Array.FindIndex(ThemeKeys, key => string.Equals(key, theme.Name, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                _themeCombo.SelectedIndex = index;
            }
        }
        finally
        {
            _syncingTheme = false;
        }
    }
}
