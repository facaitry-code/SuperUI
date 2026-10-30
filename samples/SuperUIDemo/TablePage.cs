using System.Drawing;
using SuperUI.Controls;
using SuperUI.Theming;

namespace SuperUIDemo;

/// <summary>
/// 表格页：SuTable 的排序、多选、复选框列、搜索过滤、行高切换与滚动。
/// </summary>
public sealed class TablePage : DemoPage
{
    private const int PadX = 24;
    private const int ToolbarTop = 80;
    private const int TableTop = 128;

    private readonly SuTable _table = new();
    private readonly SuTextBox _search = new();
    private readonly SuSwitch _multiSelect = new();
    private readonly SuSwitch _zebra = new();
    private readonly SuComboBox _density = new();
    private readonly SuLabel _status = new();
    private readonly Label _title = new();
    private readonly Label _subtitle = new();

    private readonly List<DemoRow> _source = new();
    private List<DemoRow> _filtered = new();

    private string _lastHit = string.Empty;
    private int _nextId = 1;
    private bool _layoutReady;

    public TablePage(bool showcase = false)
    {
        BuildData();
        BuildUi();
        ReloadRows();

        if (showcase)
        {
            // 文档截图用：先按金额降序排一次，再选中/勾选几行，展示排序箭头与选中态。
            _table.SortBy(4, false);
            _table.SelectIndex(1);
            _table.Rows[3].Checked = true;
            _table.Rows[6].Checked = true;
            _table.Invalidate();
            UpdateStatus();
        }

        OnPageThemeChanged();
    }

    public override string PageTitle => "表格 SuTable";

    /// <summary>表格自己铺满内容区，不需要外层滚动条。</summary>
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

    /// <summary>表格与状态栏按内容区大小铺开（页面尺寸在构造时还是 0，所以要在这里再算一次）。</summary>
    private void LayoutContent()
    {
        if (!_layoutReady)
        {
            return;
        }

        var area = DisplayRectangle;
        var width = Math.Max(240, area.Width - PadX * 2);
        var height = Math.Max(120, area.Height - TableTop - 36);

        _table.Bounds = new Rectangle(area.X + PadX, area.Y + TableTop, width, height);
        _status.Location = new Point(area.X + PadX, area.Y + area.Height - 28);
    }

    // ---------------- 演示数据 ----------------

    private static readonly string[] Names = { "张一鸣", "李思远", "王雨桐", "陈亦可", "赵南星", "周牧之", "吴知微", "徐若愚" };

    private static readonly string[] Departments = { "研发中心", "设计中心", "市场部", "客户成功", "财务部" };

    private static readonly string[] Domains = { "example.com", "mail.dev", "corp.io" };

    private void BuildData()
    {
        _source.Clear();
        _nextId = 1;

        var random = new Random(20240607);
        for (var i = 0; i < 240; i++)
        {
            _source.Add(CreateRow(random));
        }

        _filtered = new List<DemoRow>(_source);
    }

    private DemoRow CreateRow(Random random)
    {
        var name = Names[random.Next(Names.Length)];
        var id = _nextId++;

        return new DemoRow
        {
            Id = id,
            Name = name,
            Department = Departments[random.Next(Departments.Length)],
            Email = $"user{id:D3}@{Domains[random.Next(Domains.Length)]}",
            Active = random.Next(4) != 0,
            Amount = (decimal)(random.NextDouble() * 98000d),
            LastLogin = DateTime.Today.AddDays(-random.Next(0, 90)).AddHours(random.Next(0, 24)),
        };
    }

    // ---------------- 界面 ----------------

    private void BuildUi()
    {
        var origin = Origin;
        var left = origin.X + PadX;

        _title.Text = "表格 SuTable";
        _title.AutoSize = true;
        _title.UseMnemonic = false;
        _title.Location = new Point(left, origin.Y + 14);
        Controls.Add(_title);

        _subtitle.Text = "自绘表头 · 排序 · 多选 · 复选框列 · 虚拟化行绘制 · 自绘滚动条，颜色全部来自主题";
        _subtitle.AutoSize = true;
        _subtitle.UseMnemonic = false;
        _subtitle.Location = new Point(left, origin.Y + 46);
        Controls.Add(_subtitle);

        var toolbarY = origin.Y + ToolbarTop;

        _search.PlaceholderText = "搜索姓名 / 部门 / 邮箱";
        _search.AutoHeight = false;
        _search.Bounds = new Rectangle(left, toolbarY, 240, 36);
        _search.TextChanged += (_, __) => ApplyFilter();
        Controls.Add(_search);

        var add = new SuButton
        {
            Text = "新增行",
            Variant = SuButtonVariant.Primary,
            IconGlyph = "\uE710",
            Bounds = new Rectangle(left + 248, toolbarY, 96, 36),
        };
        add.Click += (_, __) => InsertRow();
        Controls.Add(add);

        var remove = new SuButton
        {
            Text = "删除选中",
            Variant = SuButtonVariant.Danger,
            Bounds = new Rectangle(left + 352, toolbarY, 104, 36),
        };
        remove.Click += (_, __) => RemoveSelected();
        Controls.Add(remove);

        var reset = new SuButton
        {
            Text = "重置数据",
            Variant = SuButtonVariant.Secondary,
            Bounds = new Rectangle(left + 464, toolbarY, 96, 36),
        };
        reset.Click += (_, __) =>
        {
            BuildData();
            ReloadRows();
        };
        Controls.Add(reset);

        _multiSelect.Text = "多选";
        _multiSelect.Checked = true;
        _multiSelect.Location = new Point(left + 572, toolbarY + 7);
        _multiSelect.CheckedChanged += (_, __) =>
        {
            _table.SelectionMode = _multiSelect.Checked
                ? SuTableSelectionMode.Multi
                : SuTableSelectionMode.Single;
            UpdateStatus();
        };
        Controls.Add(_multiSelect);

        _zebra.Text = "斑马纹";
        _zebra.Checked = true;
        _zebra.Location = new Point(left + 660, toolbarY + 7);
        _zebra.CheckedChanged += (_, __) =>
        {
            _table.ZebraStripes = _zebra.Checked;
            UpdateStatus();
        };
        Controls.Add(_zebra);

        _density.Bounds = new Rectangle(left + 760, toolbarY, 128, 36);
        _density.Items.AddRange(new object[] { "紧凑行高", "标准行高", "宽松行高" });
        _density.SelectedIndex = 1;
        _density.SelectedIndexChanged += (_, __) =>
        {
            _table.RowHeight = _density.SelectedIndex switch
            {
                0 => 28,
                2 => 46,
                _ => 36,
            };
        };
        Controls.Add(_density);

        // ---------------- 表格本体 ----------------
        BuildColumns();

        _table.ShowCheckBoxes = true;
        _table.SelectionMode = SuTableSelectionMode.Multi;
        _table.Bounds = new Rectangle(left, origin.Y + TableTop, 600, 300);

        _table.SelectionChanged += (_, __) => UpdateStatus();
        _table.CheckedChanged += (_, __) => UpdateStatus();
        _table.CellClick += (_, e) =>
        {
            var name = e.Column is null ? "行" : e.Column.Header;
            _lastHit = $"最后点击：{name} / 第 {e.RowIndex + 1} 行";
            UpdateStatus();
        };
        _table.RowActivated += (_, e) =>
        {
            _lastHit = $"双击：第 {e.RowIndex + 1} 行";
            UpdateStatus();
        };
        _table.Sorted += (_, e) =>
        {
            _lastHit = e.Cleared
                ? "已取消排序"
                : $"按「{e.Column?.Header}」{(e.Ascending ? "升序" : "降序")}";
            UpdateStatus();
        };
        Controls.Add(_table);

        _status.AutoSize = true;
        _status.UseMnemonic = false;
        _status.Location = new Point(left, origin.Y + 320);
        Controls.Add(_status);

        _layoutReady = true;
        LayoutContent();
    }

    private void BuildColumns()
    {
        _table.Columns.Add(new SuTableColumn("编号", 72) { Align = SuTableAlign.Right, Property = nameof(DemoRow.Id) });
        _table.Columns.Add(new SuTableColumn("姓名", 104) { Property = nameof(DemoRow.Name) });
        _table.Columns.Add(new SuTableColumn("部门", 104) { Property = nameof(DemoRow.Department) });
        _table.Columns.Add(new SuTableColumn("邮箱", -1) { Property = nameof(DemoRow.Email) });
        _table.Columns.Add(new SuTableColumn("金额", 112)
        {
            Align = SuTableAlign.Right,
            Property = nameof(DemoRow.Amount),
            Format = "N2",
        });
        _table.Columns.Add(new SuTableColumn("状态", 76)
        {
            Align = SuTableAlign.Center,
            Getter = row => row.Item is DemoRow model ? (model.Active ? "启用" : "停用") : string.Empty,
        });
        _table.Columns.Add(new SuTableColumn("最后登录", 148)
        {
            Property = nameof(DemoRow.LastLogin),
            Format = "yyyy-MM-dd HH:mm",
        });
    }

    // ---------------- 数据操作 ----------------

    private void ApplyFilter()
    {
        var keyword = _search.Text.Trim();
        _filtered = keyword.Length == 0
            ? new List<DemoRow>(_source)
            : _source.FindAll(row =>
                Matches(row.Name, keyword)
                || Matches(row.Department, keyword)
                || Matches(row.Email, keyword));

        ReloadRows();
    }

    private void ReloadRows()
    {
        _table.SetDataSource(_filtered);
        UpdateStatus();
    }

    /// <summary>net48 的 string 没有 Contains(string, StringComparison) 重载。</summary>
    private static bool Matches(string value, string keyword)
        => value.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;

    private void InsertRow()
    {
        var row = CreateRow(new Random(Environment.TickCount));
        _source.Insert(0, row);
        ApplyFilter();
    }

    private void RemoveSelected()
    {
        var selected = _table.SelectedRows;
        if (selected.Count == 0)
        {
            _lastHit = "没有选中行";
            UpdateStatus();
            return;
        }

        foreach (var tableRow in selected)
        {
            if (tableRow.Item is DemoRow model)
            {
                _source.Remove(model);
            }
        }

        _lastHit = $"删除了 {selected.Count} 行";
        ApplyFilter();
    }

    private void UpdateStatus()
    {
        var parts = new List<string>
        {
            $"{_table.RowCount} / {_source.Count} 行",
            $"已选 {_table.SelectedRows.Count} 行",
            $"已勾选 {CountChecked()} 行",
        };

        if (_table.SortColumnIndex >= 0)
        {
            parts.Add($"排序：{_table.Columns[_table.SortColumnIndex].Header} {(_table.SortAscending ? "↑" : "↓")}");
        }

        if (_lastHit.Length > 0)
        {
            parts.Add(_lastHit);
        }

        _status.Text = string.Join("　·　", parts);
    }

    private int CountChecked()
    {
        var count = 0;
        foreach (var row in _table.Rows)
        {
            if (row.Checked)
            {
                count++;
            }
        }

        return count;
    }

    protected override void OnPageThemeChanged()
    {
        var theme = SuThemeManager.Current;

        _title.Font = DemoFonts.Get(theme, 13f, FontStyle.Bold);
        _title.ForeColor = theme.TextPrimary;
        _subtitle.Font = DemoFonts.Get(theme, 9f);
        _subtitle.ForeColor = theme.TextSecondary;
        _status.ForeColor = theme.TextSecondary;
    }

    /// <summary>演示用数据模型：列通过 <see cref="SuTableColumn.Property"/> 反射取值。</summary>
    private sealed class DemoRow
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Department { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public bool Active { get; set; }

        public decimal Amount { get; set; }

        public DateTime LastLogin { get; set; }
    }
}
