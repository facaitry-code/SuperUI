using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Reflection;
using SuperUI.Rendering;
using SuperUI.Theming;

namespace SuperUI.Controls;

/// <summary>单元格内容的水平对齐方式。</summary>
public enum SuTableAlign
{
    /// <summary>左对齐（默认，文本列）。</summary>
    Left,

    /// <summary>居中。</summary>
    Center,

    /// <summary>右对齐（数字列）。</summary>
    Right,
}

/// <summary>表格行选择模式。</summary>
public enum SuTableSelectionMode
{
    /// <summary>不可选择。</summary>
    None,

    /// <summary>单选。</summary>
    Single,

    /// <summary>多选（Ctrl 追加 / 取消，Shift 连续区间）。</summary>
    Multi,
}

/// <summary>
/// 表格列定义：表头文字、宽度、对齐、排序开关，以及取值方式
/// （<see cref="Getter"/> 最灵活，<see cref="Property"/> 走反射，都没有时按二维数组的列序号取值）。
/// </summary>
public class SuTableColumn
{
    private string _header = string.Empty;
    private int _width = -1;
    private int _minWidth = 48;
    private SuTableAlign _align = SuTableAlign.Left;
    private bool _sortable = true;
    private bool _visible = true;
    private string? _format;
    private string? _property;
    private Func<SuTableRow, object?>? _getter;

    public SuTableColumn()
    {
    }

    public SuTableColumn(string header) => _header = header ?? string.Empty;

    public SuTableColumn(string header, int width)
    {
        _header = header ?? string.Empty;
        _width = width;
    }

    internal SuTable? Owner { get; set; }

    /// <summary>表头文字。</summary>
    [Category("SuperUI")]
    [DefaultValue("")]
    public string Header
    {
        get => _header;
        set
        {
            value ??= string.Empty;
            if (_header == value)
            {
                return;
            }

            _header = value;
            Owner?.OnColumnsChanged();
        }
    }

    /// <summary>列宽：大于 0 为固定像素；小于等于 0 表示自适应（平分剩余宽度）。</summary>
    [Category("SuperUI")]
    [DefaultValue(-1)]
    public int Width
    {
        get => _width;
        set
        {
            if (_width == value)
            {
                return;
            }

            _width = value;
            Owner?.OnColumnsChanged();
        }
    }

    /// <summary>最小列宽（自适应列也不会低于该值）。</summary>
    [Category("SuperUI")]
    [DefaultValue(48)]
    public int MinWidth
    {
        get => _minWidth;
        set
        {
            var width = Math.Max(16, value);
            if (_minWidth == width)
            {
                return;
            }

            _minWidth = width;
            Owner?.OnColumnsChanged();
        }
    }

    /// <summary>单元格与表头的水平对齐。</summary>
    [Category("SuperUI")]
    [DefaultValue(SuTableAlign.Left)]
    public SuTableAlign Align
    {
        get => _align;
        set
        {
            if (_align == value)
            {
                return;
            }

            _align = value;
            Owner?.InvalidateLayout();
        }
    }

    /// <summary>点击表头时是否参与排序。</summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    public bool Sortable
    {
        get => _sortable;
        set
        {
            if (_sortable == value)
            {
                return;
            }

            _sortable = value;
            Owner?.InvalidateLayout();
        }
    }

    /// <summary>是否显示该列。</summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    public bool Visible
    {
        get => _visible;
        set
        {
            if (_visible == value)
            {
                return;
            }

            _visible = value;
            Owner?.OnColumnsChanged();
        }
    }

    /// <summary>值格式化字符串（配合 <see cref="IFormattable"/>，例如 "N2" / "yyyy-MM-dd"）。</summary>
    [Category("SuperUI")]
    [DefaultValue(null)]
    public string? Format
    {
        get => _format;
        set
        {
            if (_format == value)
            {
                return;
            }

            _format = value;
            Owner?.InvalidateLayout();
        }
    }

    /// <summary>用反射取属性名（数据源为实体对象时最方便）。</summary>
    [Category("SuperUI")]
    [DefaultValue(null)]
    public string? Property
    {
        get => _property;
        set
        {
            if (_property == value)
            {
                return;
            }

            _property = value;
            Owner?.InvalidateLayout();
        }
    }

    /// <summary>自定义取值委托（优先级最高，入参是整行，可自行从 <see cref="SuTableRow.Item"/> 取值）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<SuTableRow, object?>? Getter
    {
        get => _getter;
        set
        {
            if (ReferenceEquals(_getter, value))
            {
                return;
            }

            _getter = value;
            Owner?.InvalidateLayout();
        }
    }
}

/// <summary>列集合：增删列时自动让所属表格重算布局。</summary>
public sealed class SuTableColumnCollection : Collection<SuTableColumn>
{
    private readonly SuTable _owner;

    internal SuTableColumnCollection(SuTable owner) => _owner = owner;

    /// <inheritdoc />
    protected override void InsertItem(int index, SuTableColumn item)
    {
        if (item is null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        if (item.Owner is not null && !ReferenceEquals(item.Owner, _owner))
        {
            throw new ArgumentException("该列已属于另一个表格。", nameof(item));
        }

        item.Owner = _owner;
        base.InsertItem(index, item);
        _owner.OnColumnsChanged();
    }

    /// <inheritdoc />
    protected override void RemoveItem(int index)
    {
        var item = this[index];
        base.RemoveItem(index);
        item.Owner = null;
        _owner.OnColumnsChanged();
    }

    /// <inheritdoc />
    protected override void ClearItems()
    {
        foreach (var item in this)
        {
            item.Owner = null;
        }

        base.ClearItems();
        _owner.OnColumnsChanged();
    }

    /// <inheritdoc />
    protected override void SetItem(int index, SuTableColumn item)
    {
        if (item is null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        var previous = this[index];
        previous.Owner = null;
        item.Owner = _owner;
        base.SetItem(index, item);
        _owner.OnColumnsChanged();
    }
}

/// <summary>
/// 表格行：可以是「单元格数组」（<see cref="Cells"/>）也可以是「实体对象」（<see cref="Item"/>），
/// 后者配合 <see cref="SuTableColumn.Property"/> / <see cref="SuTableColumn.Getter"/> 取值。
/// </summary>
public class SuTableRow
{
    public SuTableRow()
    {
    }

    public SuTableRow(params object?[] cells) => Cells = cells ?? Array.Empty<object?>();

    /// <summary>单元格原始值（与列按序号对应）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object?[]? Cells { get; set; }

    /// <summary>行对应的实体对象（列用 Property / Getter 取值时使用）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? Item { get; set; }

    /// <summary>业务附加数据。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? Tag { get; set; }

    /// <summary>复选框列的勾选状态。</summary>
    [Category("SuperUI")]
    [DefaultValue(false)]
    public bool Checked { get; set; }

    /// <summary>行是否可用（禁用行整体转灰，且不响应点击/勾选）。</summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    public bool Enabled { get; set; } = true;

    /// <summary>是否被选中（由表格维护）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsSelected { get; internal set; }
}

/// <summary>
/// SuperUI 表格：全自绘表头 + 虚拟化行绘制 + 斑马纹 + 排序 + 单选/多选 + 复选框列 +
/// 自绘滚动条（滚轮 / 拖拽 / 键盘），颜色全部取自 <see cref="SuTheme"/>，随全局主题即时换肤。
/// </summary>
[Description("SuperUI 表格：主题化表头、斑马纹、排序、多选、复选框列、自绘滚动条与键盘导航。")]
[DefaultProperty(nameof(Columns))]
public class SuTable : SuControlBase
{
    /// <summary>滚动条占位宽度。</summary>
    private const int ScrollBarWidth = 10;

    /// <summary>排序箭头预留宽度。</summary>
    private const int SortGlyphWidth = 18;

    /// <summary>复选框列宽度。</summary>
    private const int CheckBoxColumnWidth = 38;

    /// <summary>复选框边长。</summary>
    private const int CheckBoxSize = 16;

    private readonly List<SuTableRow> _rows = new();
    private readonly SuTableColumnCollection _columns;
    private readonly SuTween _hoverTween;
    private readonly List<ColumnMetrics> _metrics = new();
    private readonly Dictionary<(Type Type, string Property), PropertyInfo?> _propertyCache = new();

    private List<SuTableRow> _view = new();
    private Font? _headerFont;

    private int _rowHeight = 36;
    private int _headerHeight = 38;
    private int _radius = -1;
    private int _cellPadding = 12;
    private bool _showHeader = true;
    private bool _zebra = true;
    private bool _showRowLines = true;
    private bool _stretchLastColumn = true;
    private bool _showCheckBoxes;
    private SuTableSelectionMode _selectionMode = SuTableSelectionMode.Single;
    private string _emptyText = "暂无数据";

    private int _scrollX;
    private int _scrollY;
    private bool _vScroll;
    private bool _hScroll;
    private int _contentWidth;
    private Rectangle _headerRect;
    private Rectangle _bodyRect;
    private Rectangle _vTrack;
    private Rectangle _hTrack;

    private int _hoverRow = -1;
    private int _hoverColumn = -1;
    private int _sortColumn = -1;
    private bool _sortAscending = true;
    private int _anchorRow = -1;

    private DragMode _dragMode;
    private int _dragOffset;
    private int _resizeColumn = -1;
    private int _resizeStartX;
    private int _resizeStartWidth;

    public SuTable()
    {
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;

        _columns = new SuTableColumnCollection(this);
        _hoverTween = new SuTween();
        _hoverTween.Changed += (_, __) => Invalidate();

        ApplyTheme();
    }

    /// <summary>选中行集合发生变化。</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>复选框列勾选状态发生变化。</summary>
    public event EventHandler? CheckedChanged;

    /// <summary>单元格被单击（<see cref="SuTableCellEventArgs.ColumnIndex"/> 为 -1 表示点到了行空白处）。</summary>
    public event EventHandler<SuTableCellEventArgs>? CellClick;

    /// <summary>行被单击。</summary>
    public event EventHandler<SuTableRowEventArgs>? RowClick;

    /// <summary>行被双击（或按 Enter）。</summary>
    public event EventHandler<SuTableRowEventArgs>? RowActivated;

    /// <summary>表头被单击。</summary>
    public event EventHandler<SuTableColumnEventArgs>? HeaderClick;

    /// <summary>排序被应用（点击表头排序后触发）。</summary>
    public event EventHandler<SuTableSortedEventArgs>? Sorted;

    /// <summary>列集合。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public SuTableColumnCollection Columns => _columns;

    /// <summary>当前显示顺序下的行集合（只读，排序只影响这里的顺序）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<SuTableRow> Rows => _view;

    /// <summary>行数。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int RowCount => _view.Count;

    /// <summary>行高。</summary>
    [Category("SuperUI")]
    [DefaultValue(36)]
    public int RowHeight
    {
        get => _rowHeight;
        set
        {
            var height = Math.Max(18, value);
            if (_rowHeight == height)
            {
                return;
            }

            _rowHeight = height;
            InvalidateLayout();
        }
    }

    /// <summary>表头高度。</summary>
    [Category("SuperUI")]
    [DefaultValue(38)]
    public int HeaderHeight
    {
        get => _headerHeight;
        set
        {
            var height = Math.Max(20, value);
            if (_headerHeight == height)
            {
                return;
            }

            _headerHeight = height;
            InvalidateLayout();
        }
    }

    /// <summary>单元格左右内边距。</summary>
    [Category("SuperUI")]
    [DefaultValue(12)]
    public int CellPadding
    {
        get => _cellPadding;
        set
        {
            var padding = Math.Max(0, value);
            if (_cellPadding == padding)
            {
                return;
            }

            _cellPadding = padding;
            InvalidateLayout();
        }
    }

    /// <summary>圆角半径，-1 表示跟随主题。</summary>
    [Category("SuperUI")]
    [DefaultValue(-1)]
    public int Radius
    {
        get => _radius;
        set
        {
            if (_radius == value)
            {
                return;
            }

            _radius = value;
            InvalidateLayout();
        }
    }

    /// <summary>是否显示表头。</summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    public bool ShowHeader
    {
        get => _showHeader;
        set
        {
            if (_showHeader == value)
            {
                return;
            }

            _showHeader = value;
            InvalidateLayout();
        }
    }

    /// <summary>是否显示斑马纹。</summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    public bool ZebraStripes
    {
        get => _zebra;
        set
        {
            if (_zebra == value)
            {
                return;
            }

            _zebra = value;
            Invalidate();
        }
    }

    /// <summary>是否画行分隔线。</summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    public bool ShowRowLines
    {
        get => _showRowLines;
        set
        {
            if (_showRowLines == value)
            {
                return;
            }

            _showRowLines = value;
            Invalidate();
        }
    }

    /// <summary>没有自适应列时，是否让最后一列吃掉剩余宽度。</summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    public bool StretchLastColumn
    {
        get => _stretchLastColumn;
        set
        {
            if (_stretchLastColumn == value)
            {
                return;
            }

            _stretchLastColumn = value;
            InvalidateLayout();
        }
    }

    /// <summary>是否显示最左侧复选框列（表头复选框为三态全选）。</summary>
    [Category("SuperUI")]
    [DefaultValue(false)]
    public bool ShowCheckBoxes
    {
        get => _showCheckBoxes;
        set
        {
            if (_showCheckBoxes == value)
            {
                return;
            }

            _showCheckBoxes = value;
            InvalidateLayout();
        }
    }

    /// <summary>选择模式。</summary>
    [Category("SuperUI")]
    [DefaultValue(SuTableSelectionMode.Single)]
    public SuTableSelectionMode SelectionMode
    {
        get => _selectionMode;
        set
        {
            if (_selectionMode == value)
            {
                return;
            }

            _selectionMode = value;
            if (value == SuTableSelectionMode.None)
            {
                ClearSelection();
            }

            Invalidate();
        }
    }

    /// <summary>没有数据时的占位文字。</summary>
    [Category("SuperUI")]
    [DefaultValue("暂无数据")]
    public string EmptyText
    {
        get => _emptyText;
        set
        {
            value ??= string.Empty;
            if (_emptyText == value)
            {
                return;
            }

            _emptyText = value;
            Invalidate();
        }
    }

    /// <summary>当前排序列（-1 表示未排序）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SortColumnIndex => _sortColumn;

    /// <summary>当前是否升序。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool SortAscending => _sortAscending;

    /// <summary>第一个可见行的序号。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int FirstVisibleRow => Math.Max(0, _scrollY / Math.Max(1, _rowHeight));

    /// <summary>当前是否出现了纵向滚动条。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool HasVerticalScroll => _vScroll;

    /// <summary>当前是否出现了横向滚动条（列总宽超出可视宽度）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool HasHorizontalScroll => _hScroll;

    /// <summary>选中行（多选时返回第一行）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SuTableRow? SelectedRow
    {
        get
        {
            foreach (var row in _view)
            {
                if (row.IsSelected)
                {
                    return row;
                }
            }

            return null;
        }
    }

    /// <summary>选中行在显示顺序中的序号（无选中为 -1）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get
        {
            for (var i = 0; i < _view.Count; i++)
            {
                if (_view[i].IsSelected)
                {
                    return i;
                }
            }

            return -1;
        }
    }

    /// <summary>全部选中行。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<SuTableRow> SelectedRows
    {
        get
        {
            var result = new List<SuTableRow>();
            foreach (var row in _view)
            {
                if (row.IsSelected)
                {
                    result.Add(row);
                }
            }

            return result;
        }
    }

    // ---------------- 数据操作 ----------------

    /// <summary>追加一列。</summary>
    public SuTableColumn AddColumn(string header, int width = -1, SuTableAlign align = SuTableAlign.Left)
    {
        var column = new SuTableColumn(header, width) { Align = align };
        _columns.Add(column);
        return column;
    }

    /// <summary>追加一行（单元格数组）。</summary>
    public SuTableRow AddRow(params object?[] cells)
    {
        var row = new SuTableRow(cells);
        _rows.Add(row);
        RefreshData();
        return row;
    }

    /// <summary>追加一行（实体对象）。</summary>
    public SuTableRow AddItem(object? item)
    {
        var row = new SuTableRow { Item = item };
        _rows.Add(row);
        RefreshData();
        return row;
    }

    /// <summary>整体替换为二维单元格数组。</summary>
    public void SetRows(IEnumerable<object?[]> rows)
    {
        _rows.Clear();
        foreach (var cells in rows)
        {
            _rows.Add(new SuTableRow(cells));
        }

        RefreshData();
    }

    /// <summary>整体替换为实体对象集合（列用 <see cref="SuTableColumn.Property"/> 或 <see cref="SuTableColumn.Getter"/> 取值）。</summary>
    public void SetDataSource<TRow>(IEnumerable<TRow> items)
    {
        _rows.Clear();
        foreach (var item in items)
        {
            _rows.Add(new SuTableRow { Item = item });
        }

        RefreshData();
    }

    /// <summary>删除一行。</summary>
    public void RemoveRow(SuTableRow row)
    {
        if (row is null)
        {
            return;
        }

        _rows.Remove(row);
        RefreshData();
    }

    /// <summary>清空所有行。</summary>
    public void ClearRows()
    {
        if (_rows.Count == 0)
        {
            return;
        }

        _rows.Clear();
        RefreshData();
    }

    /// <summary>数据变化后重建视图（保持当前排序）。</summary>
    public void RefreshData()
    {
        RebuildView();
        _hoverRow = -1;
        InvalidateLayout();
    }

    /// <summary>取一行的单元格显示文本。</summary>
    public string GetCellText(SuTableRow row, int columnIndex)
    {
        if (row is null || columnIndex < 0 || columnIndex >= _columns.Count)
        {
            return string.Empty;
        }

        return GetCellText(row, _columns[columnIndex], columnIndex);
    }

    // ---------------- 选择 ----------------

    /// <summary>选中指定序号的行。</summary>
    public void SelectIndex(int index)
    {
        if (index < 0 || index >= _view.Count || _selectionMode == SuTableSelectionMode.None)
        {
            return;
        }

        SetSingleSelection(_view[index]);
        _anchorRow = index;
        EnsureVisible(index);
    }

    /// <summary>全选（仅多选模式）。</summary>
    public void SelectAll()
    {
        if (_selectionMode != SuTableSelectionMode.Multi || _view.Count == 0)
        {
            return;
        }

        var changed = false;
        foreach (var row in _rows)
        {
            if (!row.IsSelected)
            {
                row.IsSelected = true;
                changed = true;
            }
        }

        if (changed)
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    /// <summary>清空选择。</summary>
    public void ClearSelection()
    {
        var changed = false;
        foreach (var row in _rows)
        {
            if (row.IsSelected)
            {
                row.IsSelected = false;
                changed = true;
            }
        }

        _anchorRow = -1;
        if (changed)
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    /// <summary>勾选 / 取消勾选所有行（<paramref name="value"/> 为 null 时反转）。</summary>
    public void CheckAll(bool? value = true)
    {
        if (_rows.Count == 0)
        {
            return;
        }

        var changed = false;
        foreach (var row in _rows)
        {
            var target = value ?? AllChecked() != true;
            if (row.Checked != target)
            {
                row.Checked = target;
                changed = true;
            }
        }

        if (changed)
        {
            CheckedChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    // ---------------- 排序 ----------------

    /// <summary>按列排序。</summary>
    public void SortBy(int columnIndex, bool ascending = true)
    {
        if (columnIndex < 0 || columnIndex >= _columns.Count)
        {
            return;
        }

        _sortColumn = columnIndex;
        _sortAscending = ascending;
        ApplySort();
        Sorted?.Invoke(this, new SuTableSortedEventArgs(_columns[columnIndex], columnIndex, ascending, false));
    }

    /// <summary>点击表头的排序切换：同列反向，换列则升序。</summary>
    public void ToggleSort(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= _columns.Count)
        {
            return;
        }

        var ascending = columnIndex == _sortColumn ? !_sortAscending : true;
        SortBy(columnIndex, ascending);
    }

    /// <summary>取消排序，恢复添加顺序。</summary>
    public void ClearSort()
    {
        if (_sortColumn < 0)
        {
            return;
        }

        _sortColumn = -1;
        _sortAscending = true;
        _view = new List<SuTableRow>(_rows);
        InvalidateLayout();
        Sorted?.Invoke(this, new SuTableSortedEventArgs(null, -1, true, true));
    }

    // ---------------- 布局查询 ----------------

    /// <summary>把每个可见列按内容自适应宽度（会取消该列的自适应设置）。</summary>
    public void AutoSizeColumns()
    {
        var headerFont = _headerFont ?? Font;
        for (var i = 0; i < _columns.Count; i++)
        {
            var column = _columns[i];
            var width = TextRenderer.MeasureText(
                            column.Header,
                            headerFont,
                            new Size(int.MaxValue, int.MaxValue),
                            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width
                        + SortGlyphWidth + _cellPadding;

            var count = Math.Min(_view.Count, 500);
            for (var r = 0; r < count; r++)
            {
                var text = GetCellText(_view[r], column, i);
                if (text.Length == 0)
                {
                    continue;
                }

                var measured = TextRenderer.MeasureText(
                                   text,
                                   Font,
                                   new Size(int.MaxValue, int.MaxValue),
                                   TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width
                               + _cellPadding * 2;

                width = Math.Max(width, measured);
            }

            column.Width = SuMath.Clamp(width, column.MinWidth, 600);
        }

        InvalidateLayout();
    }

    /// <summary>把指定行滚动到可见区域。</summary>
    public void EnsureVisible(int index)
    {
        if (index < 0 || index >= _view.Count || _bodyRect.Height <= 0)
        {
            return;
        }

        var top = index * _rowHeight;
        var bottom = top + _rowHeight;

        if (top < _scrollY)
        {
            ScrollTo(_scrollX, top);
        }
        else if (bottom > _scrollY + _bodyRect.Height)
        {
            ScrollTo(_scrollX, bottom - _bodyRect.Height);
        }
    }

    /// <summary>滚动到指定位置。</summary>
    public void ScrollTo(int x, int y)
    {
        RebuildLayout();
        var maxX = Math.Max(0, _contentWidth - _bodyRect.Width);
        var maxY = Math.Max(0, _view.Count * Math.Max(1, _rowHeight) - _bodyRect.Height);
        var targetX = SuMath.Clamp(x, 0, maxX);
        var targetY = SuMath.Clamp(y, 0, maxY);

        if (targetX == _scrollX && targetY == _scrollY)
        {
            return;
        }

        _scrollX = targetX;
        _scrollY = targetY;
        Invalidate();
    }

    /// <summary>鼠标位置所在的行序号（不在数据区返回 -1）。</summary>
    public int RowIndexAt(Point point)
    {
        if (!_bodyRect.Contains(point))
        {
            return -1;
        }

        var index = (point.Y - _bodyRect.Y + _scrollY) / Math.Max(1, _rowHeight);
        return index >= 0 && index < _view.Count ? index : -1;
    }

    /// <summary>鼠标位置所在的列序号（不在列上返回 -1）。</summary>
    public int ColumnIndexAt(Point point)
    {
        var bounds = _bodyRect.Contains(point)
            ? _bodyRect
            : _headerRect.Contains(point)
                ? _headerRect
                : Rectangle.Empty;

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return -1;
        }

        foreach (var metrics in _metrics)
        {
            var rect = new Rectangle(ContentOrigin + metrics.X, bounds.Y, metrics.Width, bounds.Height);
            var visible = Rectangle.Intersect(rect, bounds);
            if (visible.Contains(point))
            {
                return metrics.Index;
            }
        }

        return -1;
    }

    // ---------------- 主题 ----------------

    /// <inheritdoc />
    public override void OnThemeChanged()
    {
        ApplyTheme();
        InvalidateLayout();
    }

    /// <inheritdoc />
    protected override void ApplyTheme()
    {
        base.ApplyTheme();
        RebuildHeaderFont();
    }

    private void RebuildHeaderFont()
    {
        var font = Font;
        if (font is null)
        {
            return;
        }

        Font created;
        try
        {
            created = new Font(font.FontFamily, font.Size, FontStyle.Bold, GraphicsUnit.Point);
        }
        catch (ArgumentException)
        {
            created = new Font(FontFamily.GenericSansSerif, font.Size, FontStyle.Bold, GraphicsUnit.Point);
        }

        var previous = _headerFont;
        _headerFont = created;
        previous?.Dispose();
    }

    // ---------------- 内部：视图 / 取值 ----------------

    private readonly struct ColumnMetrics
    {
        public ColumnMetrics(SuTableColumn column, int index, int x, int width)
        {
            Column = column;
            Index = index;
            X = x;
            Width = width;
        }

        public SuTableColumn Column { get; }

        public int Index { get; }

        public int X { get; }

        public int Width { get; }
    }

    private enum DragMode
    {
        None,
        ThumbV,
        ThumbH,
        ColumnResize,
    }

    /// <summary>内容区（列）的起点 X，已计入横向滚动。</summary>
    private int ContentOrigin => _bodyRect.X - _scrollX;

    private void RebuildView()
    {
        _view = new List<SuTableRow>(_rows);

        if (_sortColumn < 0 || _sortColumn >= _columns.Count)
        {
            _sortColumn = _sortColumn >= _columns.Count ? -1 : _sortColumn;
            return;
        }

        ApplySort();
    }

    private void ApplySort()
    {
        if (_sortColumn < 0 || _sortColumn >= _columns.Count)
        {
            _view = new List<SuTableRow>(_rows);
            return;
        }

        var column = _columns[_sortColumn];
        var index = _sortColumn;
        var ascending = _sortAscending;

        // OrderBy 是稳定排序：相同键保持原有顺序。
        _view = ascending
            ? _view.OrderBy(row => GetSortValue(row, column, index), ValueComparer.Instance).ToList()
            : _view.OrderByDescending(row => GetSortValue(row, column, index), ValueComparer.Instance).ToList();
    }

    private object? GetSortValue(SuTableRow row, SuTableColumn column, int columnIndex)
    {
        if (column.Getter is not null)
        {
            return column.Getter(row);
        }

        if (row.Cells is not null && columnIndex < row.Cells.Length)
        {
            return row.Cells[columnIndex];
        }

        return GetPropertyValue(row.Item, column.Property);
    }

    private object? GetCellValue(SuTableRow row, SuTableColumn column, int columnIndex)
    {
        if (column.Getter is not null)
        {
            return column.Getter(row);
        }

        if (!string.IsNullOrEmpty(column.Property))
        {
            var value = GetPropertyValue(row.Item, column.Property);
            if (value is not null || row.Item is not null)
            {
                return value;
            }
        }

        if (row.Cells is not null && columnIndex < row.Cells.Length)
        {
            return row.Cells[columnIndex];
        }

        return null;
    }

    private object? GetPropertyValue(object? target, string? property)
    {
        var name = property ?? string.Empty;
        if (target is null || name.Length == 0)
        {
            return null;
        }

        var key = (target.GetType(), name);
        if (!_propertyCache.TryGetValue(key, out var info))
        {
            info = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            _propertyCache[key] = info;
        }

        return info?.GetValue(target, null);
    }

    private string GetCellText(SuTableRow row, SuTableColumn column, int columnIndex)
    {
        var value = GetCellValue(row, column, columnIndex);
        if (value is null)
        {
            return string.Empty;
        }

        if (column.Format is not null && value is IFormattable formattable)
        {
            return formattable.ToString(column.Format, CultureInfo.CurrentCulture);
        }

        return Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;
    }

    /// <summary>值比较：数字按数值、日期按时间、其余按当前区域性字符串比较。</summary>
    private sealed class ValueComparer : IComparer<object?>
    {
        public static readonly ValueComparer Instance = new();

        public int Compare(object? x, object? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            if (x is IConvertible && y is IConvertible)
            {
                var xCode = ((IConvertible)x).GetTypeCode();
                var yCode = ((IConvertible)y).GetTypeCode();

                if (IsNumeric(xCode) && IsNumeric(yCode))
                {
                    return Convert.ToDouble(x, CultureInfo.CurrentCulture)
                        .CompareTo(Convert.ToDouble(y, CultureInfo.CurrentCulture));
                }

                if (xCode == TypeCode.DateTime && yCode == TypeCode.DateTime)
                {
                    return ((DateTime)x).CompareTo((DateTime)y);
                }
            }

            if (x is IComparable comparable && x.GetType() == y.GetType())
            {
                return comparable.CompareTo(y);
            }

            return string.Compare(
                Convert.ToString(x, CultureInfo.CurrentCulture),
                Convert.ToString(y, CultureInfo.CurrentCulture),
                StringComparison.CurrentCulture);
        }

        private static bool IsNumeric(TypeCode code)
            => code is TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16
                or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64
                or TypeCode.Single or TypeCode.Double or TypeCode.Decimal;
    }

    // ---------------- 内部：布局 ----------------

    /// <summary>列变化（增删列、改宽度/表头）后调用。</summary>
    internal void OnColumnsChanged()
    {
        if (_sortColumn >= _columns.Count)
        {
            _sortColumn = -1;
        }

        _metrics.Clear();
        _hoverColumn = -1;
        RebuildView();
        InvalidateLayout();
    }

    /// <summary>只重算布局并重绘。</summary>
    internal void InvalidateLayout()
    {
        RebuildLayout();
        Invalidate();
    }

    private Rectangle GetInnerRect()
    {
        // 1px 外框 + 1px 呼吸边，保证圆角不会切到首行文字。
        var inner = new Rectangle(2, 2, Math.Max(0, Width - 4), Math.Max(0, Height - 4));
        return new Rectangle(
            inner.X + Padding.Left,
            inner.Y + Padding.Top,
            Math.Max(0, inner.Width - Padding.Horizontal),
            Math.Max(0, inner.Height - Padding.Vertical));
    }

    private void RebuildLayout()
    {
        var inner = GetInnerRect();
        var showHeader = _showHeader && _columns.Count > 0;
        var headerHeight = showHeader ? Math.Max(20, _headerHeight) : 0;

        _headerRect = showHeader
            ? new Rectangle(inner.X, inner.Y, inner.Width, headerHeight)
            : Rectangle.Empty;

        _vScroll = false;
        _hScroll = false;
        _bodyRect = Rectangle.Empty;
        _contentWidth = 0;

        for (var pass = 0; pass < 3; pass++)
        {
            var bodyHeight = Math.Max(0, inner.Height - headerHeight - (_hScroll ? ScrollBarWidth : 0));
            var needV = _view.Count * Math.Max(1, _rowHeight) > bodyHeight;
            var availableWidth = Math.Max(0, inner.Width - (needV ? ScrollBarWidth : 0));

            ComputeColumnWidths(availableWidth);

            var needH = _contentWidth > availableWidth;

            if (needV == _vScroll && needH == _hScroll)
            {
                _bodyRect = new Rectangle(inner.X, inner.Y + headerHeight, availableWidth, bodyHeight);
                break;
            }

            _vScroll = needV;
            _hScroll = needH;

            if (pass == 2)
            {
                ComputeColumnWidths(availableWidth);
                _bodyRect = new Rectangle(inner.X, inner.Y + headerHeight, availableWidth, bodyHeight);
            }
        }

        _vTrack = new Rectangle(
            inner.Right - ScrollBarWidth,
            inner.Y + headerHeight,
            ScrollBarWidth,
            Math.Max(0, inner.Height - headerHeight - (_hScroll ? ScrollBarWidth : 0)));

        _hTrack = new Rectangle(
            inner.X,
            inner.Bottom - ScrollBarWidth,
            Math.Max(0, inner.Width - (_vScroll ? ScrollBarWidth : 0)),
            ScrollBarWidth);

        var maxScrollY = Math.Max(0, _view.Count * Math.Max(1, _rowHeight) - _bodyRect.Height);
        var maxScrollX = Math.Max(0, _contentWidth - _bodyRect.Width);
        _scrollY = SuMath.Clamp(_scrollY, 0, maxScrollY);
        _scrollX = SuMath.Clamp(_scrollX, 0, maxScrollX);
    }

    private void ComputeColumnWidths(int availableWidth)
    {
        _metrics.Clear();

        var checkboxWidth = _showCheckBoxes ? CheckBoxColumnWidth : 0;
        var available = Math.Max(0, availableWidth - checkboxWidth);

        var fixedWidth = 0;
        var fillCount = 0;

        for (var i = 0; i < _columns.Count; i++)
        {
            var column = _columns[i];
            if (!column.Visible)
            {
                continue;
            }

            var min = Math.Max(16, column.MinWidth);
            if (column.Width > 0)
            {
                fixedWidth += Math.Max(min, column.Width);
            }
            else
            {
                fillCount++;
                fixedWidth += min;
            }
        }

        var remaining = available - fixedWidth;
        var fillExtra = fillCount > 0 && remaining > 0 ? remaining / fillCount : 0;

        var x = checkboxWidth;
        var lastVisibleIndex = -1;

        for (var i = 0; i < _columns.Count; i++)
        {
            var column = _columns[i];
            if (!column.Visible)
            {
                continue;
            }

            var min = Math.Max(16, column.MinWidth);
            var width = column.Width > 0 ? Math.Max(min, column.Width) : min + fillExtra;

            _metrics.Add(new ColumnMetrics(column, i, x, width));
            x += width;
            lastVisibleIndex = _metrics.Count - 1;
        }

        // 没有自适应列时，让最后一列吃掉剩余宽度（填满表面，避免右侧空一条）。
        if (fillCount == 0 && lastVisibleIndex >= 0 && remaining > 0 && _stretchLastColumn)
        {
            var last = _metrics[lastVisibleIndex];
            _metrics[lastVisibleIndex] = new ColumnMetrics(last.Column, last.Index, last.X, last.Width + remaining);
            x += remaining;
        }

        // 有自适应列但没有剩余空间时，允许横向滚动（宽度不小于最小宽度）。
        _contentWidth = x;
    }

    // ---------------- 绘制 ----------------

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        SuDraw.SetupQuality(graphics);
        RebuildLayout();

        var theme = Theme;
        using (var clear = new SolidBrush(BackColor))
        {
            graphics.FillRectangle(clear, ClientRectangle);
        }

        var radius = _radius >= 0 ? _radius : theme.Radius;
        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        var surface = Enabled ? theme.Surface : theme.SurfaceAlt;

        using (var path = SuDraw.RoundedRect(rect, radius))
        using (var brush = new SolidBrush(surface))
        {
            graphics.FillPath(brush, path);

            DrawRows(graphics, theme, surface);
            DrawHeader(graphics, theme);
            DrawEmptyState(graphics, theme);
            DrawScrollBars(graphics, theme);

            using var pen = new Pen(Focused ? theme.Primary : theme.Border, Focused ? 1.4f : 1f);
            graphics.DrawPath(pen, path);
        }
    }

    private void DrawHeader(Graphics graphics, SuTheme theme)
    {
        if (_headerRect.Width <= 0 || _headerRect.Height <= 0)
        {
            return;
        }

        var state = graphics.Save();
        graphics.SetClip(_headerRect, CombineMode.Intersect);

        using (var brush = new SolidBrush(theme.SurfaceAlt))
        {
            graphics.FillRectangle(brush, _headerRect);
        }

        if (_showCheckBoxes)
        {
            var box = new Rectangle(
                ContentOrigin + (CheckBoxColumnWidth - CheckBoxSize) / 2,
                _headerRect.Y + (_headerRect.Height - CheckBoxSize) / 2,
                CheckBoxSize,
                CheckBoxSize);

            if (box.Right > _headerRect.Left)
            {
                DrawCheckBox(graphics, theme, box, AllChecked(), true, true);
            }
        }

        var headerFont = _headerFont ?? Font;
        var padding = Math.Max(0, _cellPadding);

        foreach (var metrics in _metrics)
        {
            var column = metrics.Column;
            var cellRect = new Rectangle(ContentOrigin + metrics.X, _headerRect.Y, metrics.Width, _headerRect.Height);
            if (cellRect.Right <= _headerRect.Left || cellRect.Left >= _headerRect.Right)
            {
                continue;
            }

            var sorted = metrics.Index == _sortColumn && _sortColumn >= 0;
            var hovered = metrics.Index == _hoverColumn && column.Sortable;

            if (hovered)
            {
                using var hoverBrush = new SolidBrush(SuDraw.Blend(theme.SurfaceAlt, theme.Surface, 0.55f));
                graphics.FillRectangle(hoverBrush, cellRect);
            }

            var reserve = column.Sortable ? SortGlyphWidth : 0;
            var textRect = new Rectangle(
                cellRect.X + padding,
                cellRect.Y,
                Math.Max(0, cellRect.Width - padding * 2 - reserve),
                cellRect.Height);

            if (textRect.Width > 0)
            {
                TextRenderer.DrawText(
                    graphics,
                    column.Header,
                    headerFont,
                    textRect,
                    sorted ? theme.Primary : hovered ? theme.TextPrimary : theme.TextSecondary,
                    GetTextFlags(column.Align));
            }

            if (sorted)
            {
                DrawSortGlyph(graphics, cellRect, _sortAscending, theme.Primary);
            }
        }

        graphics.Restore(state);

        using (var pen = new Pen(theme.Border, 1f))
        {
            graphics.DrawLine(
                pen,
                _headerRect.Left,
                _headerRect.Bottom - 0.5f,
                _headerRect.Right,
                _headerRect.Bottom - 0.5f);
        }
    }

    private void DrawRows(Graphics graphics, SuTheme theme, Color surface)
    {
        if (_view.Count == 0 || _bodyRect.Width <= 0 || _bodyRect.Height <= 0)
        {
            return;
        }

        var rowHeight = Math.Max(1, _rowHeight);
        var first = Math.Max(0, _scrollY / rowHeight);
        var last = Math.Min(_view.Count - 1, (_scrollY + _bodyRect.Height) / rowHeight);

        var hoverAmount = Math.Max(0.45f, _hoverTween.Value);
        var padding = Math.Max(0, _cellPadding);

        var state = graphics.Save();
        graphics.SetClip(_bodyRect, CombineMode.Intersect);

        using var linePen = new Pen(SuDraw.Alpha(theme.Border, 170), 1f);

        for (var index = first; index <= last; index++)
        {
            var row = _view[index];
            var top = _bodyRect.Y + index * rowHeight - _scrollY;
            var rowRect = new Rectangle(_bodyRect.X, top, _bodyRect.Width, rowHeight);

            var background = _zebra && (index % 2 == 1) ? theme.SurfaceAlt : surface;
            if (row.IsSelected)
            {
                background = theme.PrimarySoft;
            }
            else if (index == _hoverRow && row.Enabled)
            {
                // 悬停用主色浅底的一半叠上去：深色/浅色主题下都比 SurfaceAlt 明显，
                // 又比选中态（整块 PrimarySoft）淡，两种状态不会混淆。
                background = SuDraw.Blend(background, theme.PrimarySoft, 0.35f + 0.2f * hoverAmount);
            }

            if (!row.Enabled)
            {
                background = SuDraw.Blend(background, theme.Background, 0.45f);
            }

            using (var brush = new SolidBrush(background))
            {
                graphics.FillRectangle(brush, rowRect);
            }

            if (_showRowLines && index > 0)
            {
                graphics.DrawLine(linePen, rowRect.Left, rowRect.Top + 0.5f, rowRect.Right, rowRect.Top + 0.5f);
            }

            if (_showCheckBoxes)
            {
                var box = new Rectangle(
                    ContentOrigin + (CheckBoxColumnWidth - CheckBoxSize) / 2,
                    top + (rowHeight - CheckBoxSize) / 2,
                    CheckBoxSize,
                    CheckBoxSize);

                if (box.Right > _bodyRect.Left)
                {
                    DrawCheckBox(graphics, theme, box, row.Checked, row.Enabled, index == _hoverRow);
                }
            }

            var foreground = !row.Enabled
                ? theme.TextDisabled
                : row.IsSelected
                    ? theme.Primary
                    : theme.TextPrimary;

            foreach (var metrics in _metrics)
            {
                var cellRect = new Rectangle(ContentOrigin + metrics.X, top, metrics.Width, rowHeight);
                if (cellRect.Right <= _bodyRect.Left || cellRect.Left >= _bodyRect.Right)
                {
                    continue;
                }

                var textRect = new Rectangle(
                    cellRect.X + padding,
                    cellRect.Y,
                    Math.Max(0, cellRect.Width - padding * 2),
                    cellRect.Height);

                if (textRect.Width <= 0)
                {
                    continue;
                }

                var text = GetCellText(row, metrics.Column, metrics.Index);
                if (text.Length == 0)
                {
                    continue;
                }

                TextRenderer.DrawText(graphics, text, Font, textRect, foreground, GetTextFlags(metrics.Column.Align));
            }
        }

        graphics.Restore(state);
    }

    private void DrawEmptyState(Graphics graphics, SuTheme theme)
    {
        if (_view.Count > 0 || _bodyRect.Width <= 0 || _bodyRect.Height <= 0 || _emptyText.Length == 0)
        {
            return;
        }

        TextRenderer.DrawText(
            graphics,
            _emptyText,
            Font,
            _bodyRect,
            theme.TextPlaceholder,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    private void DrawScrollBars(Graphics graphics, SuTheme theme)
    {
        var color = SuDraw.Alpha(theme.BorderStrong, 210);

        if (_vScroll && _vTrack.Height > 0)
        {
            var thumb = GetVThumbRect();
            if (thumb.Height > 0)
            {
                using var path = SuDraw.RoundedRect(thumb, 2f);
                using var brush = new SolidBrush(color);
                graphics.FillPath(brush, path);
            }
        }

        if (_hScroll && _hTrack.Width > 0)
        {
            var thumb = GetHThumbRect();
            if (thumb.Width > 0)
            {
                using var path = SuDraw.RoundedRect(thumb, 2f);
                using var brush = new SolidBrush(color);
                graphics.FillPath(brush, path);
            }
        }
    }

    private RectangleF GetVThumbRect()
    {
        var contentHeight = Math.Max(1, _view.Count * Math.Max(1, _rowHeight));
        var trackHeight = _vTrack.Height;
        if (trackHeight <= 0)
        {
            return RectangleF.Empty;
        }

        var ratio = SuMath.Clamp((float)(_bodyRect.Height / (double)contentHeight), 0.06f, 1f);
        var thumbHeight = Math.Min(trackHeight, Math.Max(28f, trackHeight * ratio));
        var maxScroll = Math.Max(1, contentHeight - _bodyRect.Height);
        var offset = (trackHeight - thumbHeight) * (_scrollY / (double)maxScroll);

        return new RectangleF(_vTrack.Right - 6f, _vTrack.Y + (float)offset, 4f, thumbHeight);
    }

    private RectangleF GetHThumbRect()
    {
        var contentWidth = Math.Max(1, _contentWidth);
        var trackWidth = _hTrack.Width;
        if (trackWidth <= 0)
        {
            return RectangleF.Empty;
        }

        var ratio = SuMath.Clamp((float)(_bodyRect.Width / (double)contentWidth), 0.06f, 1f);
        var thumbWidth = Math.Min(trackWidth, Math.Max(28f, trackWidth * ratio));
        var maxScroll = Math.Max(1, contentWidth - _bodyRect.Width);
        var offset = (trackWidth - thumbWidth) * (_scrollX / (double)maxScroll);

        return new RectangleF(_hTrack.X + (float)offset, _hTrack.Bottom - 6f, thumbWidth, 4f);
    }

    private static TextFormatFlags GetTextFlags(SuTableAlign align)
    {
        var flags = TextFormatFlags.VerticalCenter
                    | TextFormatFlags.SingleLine
                    | TextFormatFlags.EndEllipsis
                    | TextFormatFlags.NoPadding
                    | TextFormatFlags.NoPrefix;

        flags |= align switch
        {
            SuTableAlign.Center => TextFormatFlags.HorizontalCenter,
            SuTableAlign.Right => TextFormatFlags.Right,
            _ => TextFormatFlags.Left,
        };

        return flags;
    }

    private static void DrawSortGlyph(Graphics graphics, Rectangle cellRect, bool ascending, Color color)
    {
        var centerX = cellRect.Right - SortGlyphWidth / 2f - 4f;
        var centerY = cellRect.Top + cellRect.Height / 2f;

        using var path = new GraphicsPath();
        if (ascending)
        {
            path.AddPolygon(new[]
            {
                new PointF(centerX - 4f, centerY + 2.5f),
                new PointF(centerX + 4f, centerY + 2.5f),
                new PointF(centerX, centerY - 3.5f),
            });
        }
        else
        {
            path.AddPolygon(new[]
            {
                new PointF(centerX - 4f, centerY - 2.5f),
                new PointF(centerX + 4f, centerY - 2.5f),
                new PointF(centerX, centerY + 3.5f),
            });
        }

        using var brush = new SolidBrush(color);
        graphics.FillPath(brush, path);
    }

    private static void DrawCheckBox(Graphics graphics, SuTheme theme, Rectangle box, bool? state, bool enabled, bool hovered)
    {
        var rect = new RectangleF(box.X + 0.5f, box.Y + 0.5f, box.Width - 1f, box.Height - 1f);
        var fill = state == true
            ? (enabled ? theme.Primary : theme.PrimaryDisabled)
            : theme.Surface;
        var border = state == true
            ? (enabled ? theme.Primary : theme.PrimaryDisabled)
            : hovered && enabled
                ? theme.BorderStrong
                : theme.Border;

        using var path = SuDraw.RoundedRect(rect, 3.5f);
        using (var brush = new SolidBrush(fill))
        {
            graphics.FillPath(brush, path);
        }

        using (var pen = new Pen(border, 1.2f))
        {
            graphics.DrawPath(pen, path);
        }

        if (state == true)
        {
            using var checkPen = new Pen(theme.TextOnAccent, 1.6f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round,
            };

            graphics.DrawLines(
                checkPen,
                new[]
                {
                    new PointF(box.X + 4.2f, box.Y + box.Height / 2f),
                    new PointF(box.X + 6.8f, box.Y + box.Height - 4.2f),
                    new PointF(box.X + box.Width - 3.8f, box.Y + 4.2f),
                });
        }
        else if (state is null)
        {
            using var dashPen = new Pen(enabled ? theme.Primary : theme.TextDisabled, 1.6f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
            };

            var y = box.Y + box.Height / 2f;
            graphics.DrawLine(dashPen, box.X + 4f, y, box.Right - 4f, y);
        }
    }

    // ---------------- 交互 ----------------

    /// <inheritdoc />
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        RebuildLayout();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        RebuildHeaderFont();
        RebuildLayout();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        switch (_dragMode)
        {
            case DragMode.ColumnResize:
                ResizeColumnTo(e.X);
                return;

            case DragMode.ThumbV:
                DragVerticalThumb(e.Y);
                return;

            case DragMode.ThumbH:
                DragHorizontalThumb(e.X);
                return;
        }

        var overHeader = _headerRect.Contains(e.Location);
        var rowIndex = overHeader ? -1 : RowIndexAt(e.Location);
        var columnIndex = overHeader || _bodyRect.Contains(e.Location) ? ColumnIndexAt(e.Location) : -1;
        var resizeColumn = overHeader ? GetResizeColumn(e.X) : -1;

        if (rowIndex != _hoverRow)
        {
            _hoverRow = rowIndex;
            _hoverTween.Snap(0f);
            _hoverTween.To(1f, 110);
        }

        if (columnIndex != _hoverColumn)
        {
            _hoverColumn = columnIndex;
        }

        Cursor = resizeColumn >= 0
            ? Cursors.SizeWE
            : overHeader && columnIndex >= 0 && _columns[columnIndex].Sortable
                ? Cursors.Hand
                : Cursors.Default;

        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        if (_hoverRow != -1 || _hoverColumn != -1)
        {
            _hoverRow = -1;
            _hoverColumn = -1;
            _hoverTween.To(0f, 140);
            Invalidate();
        }

        base.OnMouseLeave(e);
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        RebuildLayout();

        if (e.Button == MouseButtons.Right)
        {
            var index = RowIndexAt(e.Location);
            if (index >= 0 && !_view[index].IsSelected)
            {
                HandleRowSelection(index, Keys.None);
            }

            return;
        }

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        // 滚动条
        if (_vScroll)
        {
            var thumb = GetVThumbRect();
            if (thumb.Contains(e.Location))
            {
                _dragMode = DragMode.ThumbV;
                _dragOffset = e.Y - (int)thumb.Top;
                Capture = true;
                return;
            }

            if (_vTrack.Contains(e.Location))
            {
                ScrollRows((e.Y < thumb.Top ? -1 : 1) * Math.Max(1, _bodyRect.Height / Math.Max(1, _rowHeight)));
                return;
            }
        }

        if (_hScroll)
        {
            var thumb = GetHThumbRect();
            if (thumb.Contains(e.Location))
            {
                _dragMode = DragMode.ThumbH;
                _dragOffset = e.X - (int)thumb.Left;
                Capture = true;
                return;
            }

            if (_hTrack.Contains(e.Location))
            {
                ScrollTo(_scrollX + (e.X < thumb.Left ? -1 : 1) * Math.Max(40, _bodyRect.Width / 2), _scrollY);
                return;
            }
        }

        // 表头
        if (_headerRect.Contains(e.Location))
        {
            var resizeColumn = GetResizeColumn(e.X);
            if (resizeColumn >= 0)
            {
                _dragMode = DragMode.ColumnResize;
                _resizeColumn = resizeColumn;
                _resizeStartX = e.X;
                _resizeStartWidth = FindMetricsWidth(resizeColumn);
                Capture = true;
                return;
            }

            if (_showCheckBoxes && GetHeaderCheckBoxRect().Contains(e.Location))
            {
                CheckAll();
                return;
            }

            var columnIndex = ColumnIndexAt(e.Location);
            if (columnIndex < 0)
            {
                return;
            }

            HeaderClick?.Invoke(this, new SuTableColumnEventArgs(_columns[columnIndex], columnIndex));
            if (_columns[columnIndex].Sortable)
            {
                ToggleSort(columnIndex);
            }

            return;
        }

        // 数据行
        var rowMove = RowIndexAt(e.Location);
        if (rowMove < 0)
        {
            return;
        }

        if (_showCheckBoxes && GetRowCheckBoxRect(rowMove).Contains(e.Location))
        {
            var target = _view[rowMove];
            if (target.Enabled)
            {
                target.Checked = !target.Checked;
                CheckedChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }

            return;
        }

        HandleRowSelection(rowMove, ModifierKeys);

        var columnAt = ColumnIndexAt(e.Location);
        CellClick?.Invoke(
            this,
            new SuTableCellEventArgs(
                _view[rowMove],
                rowMove,
                columnAt >= 0 ? _columns[columnAt] : null,
                columnAt));

        RowClick?.Invoke(this, new SuTableRowEventArgs(_view[rowMove], rowMove));
    }

    /// <inheritdoc />
    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_dragMode != DragMode.None)
        {
            _dragMode = DragMode.None;
            _resizeColumn = -1;
            Capture = false;
            RebuildLayout();
            Invalidate();
        }

        base.OnMouseUp(e);
    }

    /// <inheritdoc />
    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        RebuildLayout();

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        if (_headerRect.Contains(e.Location))
        {
            var columnIndex = ColumnIndexAt(e.Location);
            if (columnIndex >= 0)
            {
                AutoSizeColumn(columnIndex);
            }

            return;
        }

        var index = RowIndexAt(e.Location);
        if (index >= 0 && _view[index].Enabled)
        {
            RowActivated?.Invoke(this, new SuTableRowEventArgs(_view[index], index));
        }
    }

    /// <inheritdoc />
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        if ((ModifierKeys & Keys.Shift) != 0 && _hScroll)
        {
            ScrollTo(_scrollX - Math.Sign(e.Delta) * 60, _scrollY);
            return;
        }

        if (_vScroll)
        {
            ScrollRows(-Math.Sign(e.Delta) * 3);
        }
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData)
    {
        switch (keyData & Keys.KeyCode)
        {
            case Keys.Up:
            case Keys.Down:
            case Keys.PageUp:
            case Keys.PageDown:
            case Keys.Home:
            case Keys.End:
                return true;
        }

        return base.IsInputKey(keyData);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        var page = Math.Max(1, _bodyRect.Height / Math.Max(1, _rowHeight) - 1);

        switch (e.KeyCode)
        {
            case Keys.Down:
                MoveSelection(1);
                e.Handled = true;
                break;

            case Keys.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;

            case Keys.PageDown:
                MoveSelection(page);
                e.Handled = true;
                break;

            case Keys.PageUp:
                MoveSelection(-page);
                e.Handled = true;
                break;

            case Keys.Home:
                if (_view.Count > 0)
                {
                    SelectIndex(0);
                }

                e.Handled = true;
                break;

            case Keys.End:
                if (_view.Count > 0)
                {
                    SelectIndex(_view.Count - 1);
                }

                e.Handled = true;
                break;

            case Keys.A when e.Control:
                SelectAll();
                e.Handled = true;
                break;

            case Keys.Space:
                if (_showCheckBoxes && SelectedIndex >= 0)
                {
                    var row = _view[SelectedIndex];
                    row.Checked = !row.Checked;
                    CheckedChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                    e.Handled = true;
                }

                break;

            case Keys.Enter:
                if (SelectedIndex >= 0)
                {
                    RowActivated?.Invoke(this, new SuTableRowEventArgs(_view[SelectedIndex], SelectedIndex));
                    e.Handled = true;
                }

                break;
        }
    }

    // ---------------- 内部：交互辅助 ----------------

    private void ScrollRows(int rows)
    {
        ScrollTo(_scrollX, _scrollY + rows * Math.Max(1, _rowHeight));
    }

    private void DragVerticalThumb(int y)
    {
        var trackHeight = _vTrack.Height;
        var thumbHeight = (int)GetVThumbRect().Height;
        if (trackHeight <= thumbHeight || thumbHeight <= 0)
        {
            return;
        }

        var offset = SuMath.Clamp(y - _dragOffset - _vTrack.Y, 0, trackHeight - thumbHeight);
        var contentHeight = Math.Max(1, _view.Count * Math.Max(1, _rowHeight));
        var maxScroll = Math.Max(1, contentHeight - _bodyRect.Height);
        ScrollTo(_scrollX, (int)Math.Round(offset / (double)(trackHeight - thumbHeight) * maxScroll));
    }

    private void DragHorizontalThumb(int x)
    {
        var trackWidth = _hTrack.Width;
        var thumbWidth = (int)GetHThumbRect().Width;
        if (trackWidth <= thumbWidth || thumbWidth <= 0)
        {
            return;
        }

        var offset = SuMath.Clamp(x - _dragOffset - _hTrack.X, 0, trackWidth - thumbWidth);
        var maxScroll = Math.Max(1, _contentWidth - _bodyRect.Width);
        ScrollTo((int)Math.Round(offset / (double)(trackWidth - thumbWidth) * maxScroll), _scrollY);
    }

    private void ResizeColumnTo(int x)
    {
        if (_resizeColumn < 0 || _resizeColumn >= _columns.Count)
        {
            return;
        }

        var column = _columns[_resizeColumn];
        var width = Math.Max(column.MinWidth, _resizeStartWidth + (x - _resizeStartX));
        if (width == column.Width)
        {
            return;
        }

        column.Width = width;
    }

    private int FindMetricsWidth(int columnIndex)
    {
        foreach (var metrics in _metrics)
        {
            if (metrics.Index == columnIndex)
            {
                return metrics.Width;
            }
        }

        return _columns[columnIndex].Width > 0 ? _columns[columnIndex].Width : _columns[columnIndex].MinWidth;
    }

    private void AutoSizeColumn(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= _columns.Count)
        {
            return;
        }

        var column = _columns[columnIndex];
        var width = TextRenderer.MeasureText(
                        column.Header,
                        _headerFont ?? Font,
                        new Size(int.MaxValue, int.MaxValue),
                        TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width
                    + SortGlyphWidth + _cellPadding;

        var count = Math.Min(_view.Count, 500);
        for (var i = 0; i < count; i++)
        {
            var text = GetCellText(_view[i], column, columnIndex);
            if (text.Length == 0)
            {
                continue;
            }

            width = Math.Max(
                width,
                TextRenderer.MeasureText(
                    text,
                    Font,
                    new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width + _cellPadding * 2);
        }

        column.Width = SuMath.Clamp(width, column.MinWidth, 600);
    }

    /// <summary>表头右边界附近（±4px）返回该列序号，用于拖拽改宽。</summary>
    private int GetResizeColumn(int x)
    {
        foreach (var metrics in _metrics)
        {
            var right = ContentOrigin + metrics.X + metrics.Width;
            if (right < _headerRect.Left || right > _headerRect.Right)
            {
                continue;
            }

            if (Math.Abs(x - right) <= 4)
            {
                return metrics.Index;
            }
        }

        return -1;
    }

    private Rectangle GetHeaderCheckBoxRect()
    {
        if (!_showCheckBoxes || _headerRect.Width <= 0)
        {
            return Rectangle.Empty;
        }

        return new Rectangle(
            ContentOrigin + (CheckBoxColumnWidth - CheckBoxSize) / 2,
            _headerRect.Y + (_headerRect.Height - CheckBoxSize) / 2,
            CheckBoxSize,
            CheckBoxSize);
    }

    private Rectangle GetRowCheckBoxRect(int rowIndex)
    {
        if (!_showCheckBoxes || rowIndex < 0 || rowIndex >= _view.Count)
        {
            return Rectangle.Empty;
        }

        var top = _bodyRect.Y + rowIndex * Math.Max(1, _rowHeight) - _scrollY;
        return new Rectangle(
            ContentOrigin + (CheckBoxColumnWidth - CheckBoxSize) / 2,
            top + (Math.Max(1, _rowHeight) - CheckBoxSize) / 2,
            CheckBoxSize,
            CheckBoxSize);
    }

    private bool? AllChecked()
    {
        if (_view.Count == 0)
        {
            return false;
        }

        var checkedCount = 0;
        foreach (var row in _view)
        {
            if (row.Checked)
            {
                checkedCount++;
            }
        }

        if (checkedCount == 0)
        {
            return false;
        }

        return checkedCount == _view.Count ? true : null;
    }

    private void MoveSelection(int delta)
    {
        if (_view.Count == 0)
        {
            return;
        }

        var current = SelectedIndex;
        var start = current < 0 ? (delta > 0 ? -1 : _view.Count) : current;
        SelectIndex(SuMath.Clamp(start + delta, 0, _view.Count - 1));
    }

    private void HandleRowSelection(int index, Keys modifiers)
    {
        if (_selectionMode == SuTableSelectionMode.None || index < 0 || index >= _view.Count)
        {
            return;
        }

        var row = _view[index];
        if (!row.Enabled)
        {
            return;
        }

        if (_selectionMode == SuTableSelectionMode.Single)
        {
            SetSingleSelection(row);
            _anchorRow = index;
            return;
        }

        if ((modifiers & Keys.Control) != 0)
        {
            row.IsSelected = !row.IsSelected;
            _anchorRow = index;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
            return;
        }

        if ((modifiers & Keys.Shift) != 0 && _anchorRow >= 0 && _anchorRow < _view.Count)
        {
            var from = Math.Min(_anchorRow, index);
            var to = Math.Max(_anchorRow, index);
            var changed = false;

            foreach (var candidate in _rows)
            {
                if (candidate.IsSelected)
                {
                    candidate.IsSelected = false;
                    changed = true;
                }
            }

            for (var i = from; i <= to; i++)
            {
                if (!_view[i].IsSelected)
                {
                    _view[i].IsSelected = true;
                    changed = true;
                }
            }

            if (changed)
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }

            return;
        }

        _anchorRow = index;
        SetSingleSelection(row);
    }

    private void SetSingleSelection(SuTableRow? row)
    {
        var changed = false;
        foreach (var candidate in _rows)
        {
            var shouldSelect = ReferenceEquals(candidate, row);
            if (candidate.IsSelected != shouldSelect)
            {
                candidate.IsSelected = shouldSelect;
                changed = true;
            }
        }

        if (changed)
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hoverTween.Dispose();
            _headerFont?.Dispose();
            _headerFont = null;
            _propertyCache.Clear();
        }

        base.Dispose(disposing);
    }
}

/// <summary>行相关事件参数。</summary>
public class SuTableRowEventArgs : EventArgs
{
    public SuTableRowEventArgs(SuTableRow row, int rowIndex)
    {
        Row = row;
        RowIndex = rowIndex;
    }

    /// <summary>行对象。</summary>
    public SuTableRow Row { get; }

    /// <summary>行号（当前显示顺序）。</summary>
    public int RowIndex { get; }
}

/// <summary>单元格点击事件参数。</summary>
public class SuTableCellEventArgs : SuTableRowEventArgs
{
    public SuTableCellEventArgs(SuTableRow row, int rowIndex, SuTableColumn? column, int columnIndex)
        : base(row, rowIndex)
    {
        Column = column;
        ColumnIndex = columnIndex;
    }

    /// <summary>列定义（点到了行末空白区时为 null）。</summary>
    public SuTableColumn? Column { get; }

    /// <summary>列序号（-1 表示未命中任何列）。</summary>
    public int ColumnIndex { get; }
}

/// <summary>表头点击事件参数。</summary>
public class SuTableColumnEventArgs : EventArgs
{
    public SuTableColumnEventArgs(SuTableColumn column, int columnIndex)
    {
        Column = column;
        ColumnIndex = columnIndex;
    }

    /// <summary>被点击的列。</summary>
    public SuTableColumn Column { get; }

    /// <summary>列序号。</summary>
    public int ColumnIndex { get; }
}

/// <summary>排序事件参数。</summary>
public class SuTableSortedEventArgs : EventArgs
{
    public SuTableSortedEventArgs(SuTableColumn? column, int columnIndex, bool ascending, bool cleared)
    {
        Column = column;
        ColumnIndex = columnIndex;
        Ascending = ascending;
        Cleared = cleared;
    }

    /// <summary>排序列（取消排序时为 null）。</summary>
    public SuTableColumn? Column { get; }

    /// <summary>排序列序号（取消排序时为 -1）。</summary>
    public int ColumnIndex { get; }

    /// <summary>是否升序。</summary>
    public bool Ascending { get; }

    /// <summary>是否取消了排序。</summary>
    public bool Cleared { get; }
}
