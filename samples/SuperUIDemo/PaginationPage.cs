using System.Drawing;
using SuperUI.Controls;
using SuperUI.Theming;

namespace SuperUIDemo;

/// <summary>
/// 分页页：SuTable 客户端分页 + SuPagination（完整态 / 紧凑态）联动，
/// 演示翻页、每页条数、首末页、紧凑模式与总数文字。
/// </summary>
public sealed class PaginationPage : DemoPage
{
    private const int PadX = 24;
    private const int ToolbarTop = 80;
    private const int TableTop = 128;
    private const int PagerBottomOffset = 152;

    private readonly SuTable _table = new();
    private readonly SuPagination _pagination = new();
    private readonly SuPagination _compactPager = new();
    private readonly SuSwitch _compactSwitch = new();
    private readonly SuSwitch _firstLastSwitch = new();
    private readonly SuSwitch _pageSizeSwitch = new();
    private readonly SuButton _previous = new();
    private readonly SuButton _next = new();
    private readonly SuButton _reload = new();
    private readonly SuLabel _status = new();
    private readonly SuLabel _compactCaption = new();
    private readonly Label _title = new();
    private readonly Label _subtitle = new();

    private readonly List<object?[]> _rows = new();
    private bool _layoutReady;

    public PaginationPage()
    {
        BuildData();
        BuildUi();
        ReloadPage();
        OnPageThemeChanged();
    }

    public override string PageTitle => "分页 SuPagination";

    /// <summary>内容自己按尺寸铺开，不需要外层滚动条。</summary>
    protected override bool Scrollable => false;

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutContent();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        LayoutContent();
    }

    /// <summary>表格、分页与状态栏按内容区大小铺开（构造函数里页面尺寸还是 0，所以这里再算一次）。</summary>
    private void LayoutContent()
    {
        if (!_layoutReady)
        {
            return;
        }

        var area = DisplayRectangle;
        var width = Math.Max(320, area.Width - PadX * 2);

        // 表格按「表头 + 10 行」定高：页数变化时高度稳定，也不会在底部留一大片空白。
        // +8 是边框与行线的余量，差 2px 就会多出一条没必要的纵向滚动条。
        var maxTableHeight = Math.Max(120, (area.Y + area.Height - PagerBottomOffset) - 12 - (area.Y + TableTop));
        var tableHeight = Math.Min(maxTableHeight, 38 + 36 * 10 + 8);
        var pagerTop = area.Y + TableTop + tableHeight + 12;

        _table.Bounds = new Rectangle(area.X + PadX, area.Y + TableTop, width, tableHeight);
        _pagination.Bounds = new Rectangle(area.X + PadX, pagerTop, width, 40);
        _compactCaption.Location = new Point(area.X + PadX, pagerTop + 66);
        _compactPager.Bounds = new Rectangle(area.X + PadX + 252, pagerTop + 60, 200, 40);
        _status.Location = new Point(area.X + PadX, pagerTop + 116);

        // 宽度会影响分页是否处于紧凑模式，布局后刷新状态文字。
        UpdateStatus();
    }

    // ---------------- 演示数据 ----------------

    private static readonly string[] Names = { "张一鸣", "李思远", "王雨桐", "陈亦可", "赵南星", "周牧之", "吴知微", "徐若愚" };

    private static readonly string[] Departments = { "研发中心", "设计中心", "市场部", "客户成功", "财务部" };

    private void BuildData()
    {
        _rows.Clear();
        var random = new Random(20240611);

        for (var i = 1; i <= 240; i++)
        {
            _rows.Add(new object?[]
            {
                i,
                Names[random.Next(Names.Length)],
                Departments[random.Next(Departments.Length)],
                Math.Round(random.NextDouble() * 98000d, 2),
            });
        }
    }

    // ---------------- 界面 ----------------

    private void BuildUi()
    {
        var origin = Origin;
        var left = origin.X + PadX;

        _title.Text = "分页 SuPagination";
        _title.AutoSize = true;
        _title.UseMnemonic = false;
        _title.Location = new Point(left, origin.Y + 14);
        Controls.Add(_title);

        _subtitle.Text = "页码自动省略 · 上下页 / 首末页 · 总数与每页条数 · 宽度不足自动紧凑，颜色全部来自主题";
        _subtitle.AutoSize = true;
        _subtitle.UseMnemonic = false;
        _subtitle.Location = new Point(left, origin.Y + 46);
        Controls.Add(_subtitle);

        var toolbarY = origin.Y + ToolbarTop;

        _compactSwitch.Text = "紧凑模式";
        _compactSwitch.Location = new Point(left, toolbarY + 7);
        _compactSwitch.CheckedChanged += (_, __) =>
        {
            _pagination.Compact = _compactSwitch.Checked;
            UpdateStatus();
        };
        Controls.Add(_compactSwitch);

        _firstLastSwitch.Text = "首页 / 末页";
        _firstLastSwitch.Location = new Point(left + 128, toolbarY + 7);
        _firstLastSwitch.CheckedChanged += (_, __) =>
        {
            _pagination.ShowFirstLast = _firstLastSwitch.Checked;
            UpdateStatus();
        };
        Controls.Add(_firstLastSwitch);

        _pageSizeSwitch.Text = "每页条数";
        _pageSizeSwitch.Location = new Point(left + 272, toolbarY + 7);
        _pageSizeSwitch.CheckedChanged += (_, __) =>
        {
            _pagination.ShowPageSizeSelector = _pageSizeSwitch.Checked;
            UpdateStatus();
        };
        Controls.Add(_pageSizeSwitch);

        _previous.Text = "上一页";
        _previous.Variant = SuButtonVariant.Secondary;
        _previous.Bounds = new Rectangle(left + 404, toolbarY, 96, 36);
        _previous.Click += (_, __) => _pagination.PreviousPage();
        Controls.Add(_previous);

        _next.Text = "下一页";
        _next.Variant = SuButtonVariant.Secondary;
        _next.Bounds = new Rectangle(left + 508, toolbarY, 96, 36);
        _next.Click += (_, __) => _pagination.NextPage();
        Controls.Add(_next);

        _reload.Text = "重新加载";
        _reload.Variant = SuButtonVariant.Primary;
        _reload.IconGlyph = "\uE72C";
        _reload.Bounds = new Rectangle(left + 612, toolbarY, 116, 36);
        _reload.Click += (_, __) =>
        {
            BuildData();
            _pagination.TotalCount = _rows.Count;
            _pagination.FirstPage();
            ReloadPage();
        };
        Controls.Add(_reload);

        // ---------------- 表格 ----------------
        _table.Columns.Add(new SuTableColumn("编号", 80) { Align = SuTableAlign.Right });
        _table.Columns.Add(new SuTableColumn("姓名", 120));
        _table.Columns.Add(new SuTableColumn("部门", 140));
        _table.Columns.Add(new SuTableColumn("金额", -1) { Align = SuTableAlign.Right, Format = "N2" });
        _table.SelectionMode = SuTableSelectionMode.Single;
        _table.Bounds = new Rectangle(left, origin.Y + TableTop, 600, 320);
        Controls.Add(_table);

        // ---------------- 主分页 ----------------
        _pagination.TotalCount = _rows.Count;
        _pagination.PageSize = 10;
        _pagination.PageChanged += (_, __) => ReloadPage();
        _pagination.PageSizeChanged += (_, __) => ReloadPage();
        Controls.Add(_pagination);

        // ---------------- 紧凑态示例 ----------------
        _compactCaption.Tone = SuLabelTone.Secondary;
        _compactCaption.Text = "紧凑模式（宽度不够或 Compact = true）：";
        _compactCaption.AutoSize = true;
        _compactCaption.UseMnemonic = false;
        _compactCaption.Location = new Point(left, origin.Y + 640);
        Controls.Add(_compactCaption);

        _compactPager.Compact = true;
        _compactPager.ShowTotal = false;
        _compactPager.PageSize = 5;
        _compactPager.TotalCount = 47;
        _compactPager.PageChanged += (_, e) => _compactCaption.Text =
            $"紧凑模式：第 {e.NewPage} / {e.PageCount} 页（每页 5 条，共 {e.TotalCount} 条）";
        Controls.Add(_compactPager);

        _status.Tone = SuLabelTone.Secondary;
        _status.AutoSize = true;
        _status.UseMnemonic = false;
        _status.Location = new Point(left, origin.Y + 680);
        Controls.Add(_status);

        _layoutReady = true;
        LayoutContent();
    }

    // ---------------- 数据联动 ----------------

    private void ReloadPage()
    {
        var pageSize = _pagination.PageSize;
        var start = (_pagination.Page - 1) * pageSize;
        var slice = new List<object?[]>();

        for (var i = start; i < Math.Min(start + pageSize, _rows.Count); i++)
        {
            slice.Add(_rows[i]);
        }

        _table.SetRows(slice);
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var pageSize = _pagination.PageSize;
        var first = _rows.Count == 0 ? 0 : ((_pagination.Page - 1) * pageSize) + 1;
        var last = Math.Min(_pagination.Page * pageSize, _rows.Count);

        _status.Text = string.Format(
            "第 {0} / {1} 页　·　第 {2}~{3} 条 / 共 {4} 条　·　每页 {5} 条　·　{6}",
            _pagination.Page,
            _pagination.PageCount,
            first,
            last,
            _rows.Count,
            pageSize,
            _pagination.IsCompact ? "紧凑模式" : "完整模式");
    }

    protected override void OnPageThemeChanged()
    {
        var theme = SuThemeManager.Current;

        _title.Font = DemoFonts.Get(theme, 13f, FontStyle.Bold);
        _title.ForeColor = theme.TextPrimary;
        _subtitle.Font = DemoFonts.Get(theme, 9f);
        _subtitle.ForeColor = theme.TextSecondary;
        _compactCaption.Font = DemoFonts.Get(theme, 9f);
    }
}
