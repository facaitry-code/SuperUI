using System.ComponentModel;
using System.Drawing.Drawing2D;
using SuperUI.Rendering;
using SuperUI.Theming;

namespace SuperUI.Controls;

/// <summary>
/// SuperUI 分页：上一页 / 下一页 / 首页 / 末页、页码（中间自动省略）、总数文字、
/// 每页条数下拉，宽度不够时自动切成紧凑模式。颜色全部取自 <see cref="SuTheme"/>，随全局主题即时换肤。
/// </summary>
[Description("SuperUI 分页：页码自动省略、上下页/首末页、总数与每页条数、紧凑模式，颜色跟随全局主题。")]
[DefaultEvent(nameof(PageChanged))]
[DefaultProperty(nameof(Page))]
public class SuPagination : SuControlBase
{
    private readonly List<Part> _parts = new();
    private readonly List<int> _pageSizeOptions = new() { 10, 20, 50, 100 };
    private readonly SuTween _hoverTween;

    private int _page = 1;
    private int _pageSize = 10;
    private int _totalCount;
    private int _maxPageButtons = 7;
    private int _itemHeight = 32;
    private int _itemWidth = 32;
    private int _gap = 6;
    private int _radius = -1;
    private bool _showPrevNext = true;
    private bool _showFirstLast;
    private bool _showTotal = true;
    private bool _compact;
    private bool _autoCompact = true;
    private bool _showPageSizeSelector;
    private string _totalFormat = "共 {0} 条";
    private string _pageTextFormat = "{0} / {1}";
    private string _pageSizeFormat = "{0} 条/页";

    private Rectangle _totalRect;
    private Rectangle _comboRect;
    private int _hoverPart = -1;
    private int _pressedPart = -1;
    private bool _compactActive;

    private SuComboBox? _pageSizeCombo;
    private bool _syncingCombo;

    public SuPagination()
    {
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        AccessibleRole = AccessibleRole.ToolBar;
        Size = new Size(520, 40);

        _hoverTween = new SuTween();
        _hoverTween.Changed += (_, __) => Invalidate();

        ApplyTheme();
    }

    /// <summary>当前页发生变化（点击、键盘或程序设置都会触发）。</summary>
    public event EventHandler<SuPageChangedEventArgs>? PageChanged;

    /// <summary>每页条数发生变化。</summary>
    public event EventHandler? PageSizeChanged;

    /// <summary>当前页（从 1 开始，自动夹取到 1..PageCount）。</summary>
    [Category("SuperUI")]
    [DefaultValue(1)]
    public int Page
    {
        get => _page;
        set => SetPage(value, false);
    }

    /// <summary>每页条数（最小 1）。</summary>
    [Category("SuperUI")]
    [DefaultValue(10)]
    public int PageSize
    {
        get => _pageSize;
        set
        {
            var size = Math.Max(1, value);
            if (_pageSize == size)
            {
                return;
            }

            _pageSize = size;
            SyncPageSizeCombo();
            ClampPage(false);
            PageSizeChanged?.Invoke(this, EventArgs.Empty);
            OnLayoutChanged();
        }
    }

    /// <summary>总条数（0 表示没有数据，仍然显示「1 / 1」）。</summary>
    [Category("SuperUI")]
    [DefaultValue(0)]
    public int TotalCount
    {
        get => _totalCount;
        set
        {
            var total = Math.Max(0, value);
            if (_totalCount == total)
            {
                return;
            }

            _totalCount = total;
            ClampPage(false);
            OnLayoutChanged();
        }
    }

    /// <summary>总页数（只读，至少为 1）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int PageCount => Math.Max(1, (int)Math.Ceiling(_totalCount / (double)Math.Max(1, _pageSize)));

    /// <summary>页码按钮数量上限（含首尾，实际数量还会受可用宽度限制）。</summary>
    [Category("SuperUI")]
    [DefaultValue(7)]
    public int MaxPageButtons
    {
        get => _maxPageButtons;
        set
        {
            var count = Math.Max(3, value);
            if (_maxPageButtons == count)
            {
                return;
            }

            _maxPageButtons = count;
            OnLayoutChanged();
        }
    }

    /// <summary>按钮高度（也是页码按钮的基准宽度）。</summary>
    [Category("SuperUI")]
    [DefaultValue(32)]
    public int ItemHeight
    {
        get => _itemHeight;
        set
        {
            var height = Math.Max(18, value);
            if (_itemHeight == height)
            {
                return;
            }

            _itemHeight = height;
            OnLayoutChanged();
        }
    }

    /// <summary>页码按钮宽度。</summary>
    [Category("SuperUI")]
    [DefaultValue(32)]
    public int ItemWidth
    {
        get => _itemWidth;
        set
        {
            var width = Math.Max(20, value);
            if (_itemWidth == width)
            {
                return;
            }

            _itemWidth = width;
            OnLayoutChanged();
        }
    }

    /// <summary>按钮间距。</summary>
    [Category("SuperUI")]
    [DefaultValue(6)]
    public int Gap
    {
        get => _gap;
        set
        {
            var gap = Math.Max(0, value);
            if (_gap == gap)
            {
                return;
            }

            _gap = gap;
            OnLayoutChanged();
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
            Invalidate();
        }
    }

    /// <summary>是否显示上一页 / 下一页。</summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    public bool ShowPrevNext
    {
        get => _showPrevNext;
        set
        {
            if (_showPrevNext == value)
            {
                return;
            }

            _showPrevNext = value;
            OnLayoutChanged();
        }
    }

    /// <summary>是否显示首页 / 末页（« / »）。</summary>
    [Category("SuperUI")]
    [DefaultValue(false)]
    public bool ShowFirstLast
    {
        get => _showFirstLast;
        set
        {
            if (_showFirstLast == value)
            {
                return;
            }

            _showFirstLast = value;
            OnLayoutChanged();
        }
    }

    /// <summary>是否在右侧显示总数文字。</summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    public bool ShowTotal
    {
        get => _showTotal;
        set
        {
            if (_showTotal == value)
            {
                return;
            }

            _showTotal = value;
            OnLayoutChanged();
        }
    }

    /// <summary>强制紧凑模式：只显示 ‹ 第 N / M 页 ›。</summary>
    [Category("SuperUI")]
    [DefaultValue(false)]
    public bool Compact
    {
        get => _compact;
        set
        {
            if (_compact == value)
            {
                return;
            }

            _compact = value;
            OnLayoutChanged();
        }
    }

    /// <summary>宽度放不下页码时自动切换到紧凑模式。</summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    public bool AutoCompact
    {
        get => _autoCompact;
        set
        {
            if (_autoCompact == value)
            {
                return;
            }

            _autoCompact = value;
            OnLayoutChanged();
        }
    }

    /// <summary>是否在右侧显示「每页 N 条」下拉框。</summary>
    [Category("SuperUI")]
    [DefaultValue(false)]
    public bool ShowPageSizeSelector
    {
        get => _showPageSizeSelector;
        set
        {
            if (_showPageSizeSelector == value)
            {
                return;
            }

            _showPageSizeSelector = value;
            AttachPageSizeCombo();
            OnLayoutChanged();
        }
    }

    /// <summary>总数文字模板，`{0}` 是总条数。</summary>
    [Category("SuperUI")]
    [DefaultValue("共 {0} 条")]
    public string TotalFormat
    {
        get => _totalFormat;
        set
        {
            value ??= string.Empty;
            if (_totalFormat == value)
            {
                return;
            }

            _totalFormat = value;
            OnLayoutChanged();
        }
    }

    /// <summary>紧凑模式的文字模板，`{0}` 是当前页、`{1}` 是总页数。</summary>
    [Category("SuperUI")]
    [DefaultValue("{0} / {1}")]
    public string PageTextFormat
    {
        get => _pageTextFormat;
        set
        {
            value ??= string.Empty;
            if (_pageTextFormat == value)
            {
                return;
            }

            _pageTextFormat = value;
            OnLayoutChanged();
        }
    }

    /// <summary>每页条数下拉项的模板，`{0}` 是条数。</summary>
    [Category("SuperUI")]
    [DefaultValue("{0} 条/页")]
    public string PageSizeFormat
    {
        get => _pageSizeFormat;
        set
        {
            value ??= string.Empty;
            if (_pageSizeFormat == value)
            {
                return;
            }

            _pageSizeFormat = value;
            RebuildPageSizeItems();
            OnLayoutChanged();
        }
    }

    /// <summary>当前是否处于紧凑模式（可能是 <see cref="AutoCompact"/> 自动切的）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsCompact => _compactActive;

    // ---------------- 页操作 ----------------

    /// <summary>跳到指定页（自动夹取，越界或同页不触发事件）。</summary>
    public void GoToPage(int page) => SetPage(page, true);

    /// <summary>下一页。</summary>
    public void NextPage() => SetPage(_page + 1, true);

    /// <summary>上一页。</summary>
    public void PreviousPage() => SetPage(_page - 1, true);

    /// <summary>首页。</summary>
    public void FirstPage() => SetPage(1, true);

    /// <summary>末页。</summary>
    public void LastPage() => SetPage(PageCount, true);

    /// <summary>当前页的页码序列（-1 表示省略号），按 <see cref="MaxPageButtons"/> 开窗。</summary>
    public IReadOnlyList<int> GetPageSequence() => BuildPageSequence(PageCount, _page, _maxPageButtons);

    /// <summary>设置每页条数下拉的可选项（同时刷新下拉框）。</summary>
    public void SetPageSizeOptions(params int[] sizes)
    {
        _pageSizeOptions.Clear();
        foreach (var size in sizes)
        {
            if (size > 0 && !_pageSizeOptions.Contains(size))
            {
                _pageSizeOptions.Add(size);
            }
        }

        if (_pageSizeOptions.Count == 0)
        {
            _pageSizeOptions.Add(10);
        }

        RebuildPageSizeItems();
        OnLayoutChanged();
    }

    /// <summary>当前每页条数的可选值。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<int> PageSizeOptions => _pageSizeOptions;

    // ---------------- 主题 ----------------

    /// <inheritdoc />
    public override void OnThemeChanged()
    {
        ApplyTheme();
        OnLayoutChanged();
        Invalidate(true);
    }

    /// <inheritdoc />
    protected override void ApplyTheme()
    {
        base.ApplyTheme();
        Cursor = Cursors.Default;
    }

    // ---------------- 内部：页码与布局 ----------------

    private enum PartKind
    {
        First,
        Previous,
        Page,
        Ellipsis,
        Next,
        Last,
        CompactLabel,
    }

    private readonly struct Part
    {
        public Part(PartKind kind, int page, Rectangle rect)
        {
            Kind = kind;
            Page = page;
            Rect = rect;
        }

        public PartKind Kind { get; }

        /// <summary>页码；省略号与紧凑标签为 -1。</summary>
        public int Page { get; }

        public Rectangle Rect { get; }
    }

    private void SetPage(int page, bool byUser)
    {
        var target = SuMath.Clamp(page, 1, PageCount);
        if (target == _page)
        {
            return;
        }

        var oldPage = _page;
        _page = target;
        OnLayoutChanged();
        PageChanged?.Invoke(this, new SuPageChangedEventArgs(oldPage, target, PageCount, _totalCount, byUser));
    }

    /// <summary>总条数 / 每页条数变化后，把当前页收回到合法范围（不当作翻页动作）。</summary>
    private void ClampPage(bool byUser)
    {
        var target = SuMath.Clamp(_page, 1, PageCount);
        if (target == _page)
        {
            return;
        }

        var oldPage = _page;
        _page = target;
        PageChanged?.Invoke(this, new SuPageChangedEventArgs(oldPage, target, PageCount, _totalCount, byUser));
    }

    private void OnLayoutChanged()
    {
        RebuildParts();
        LayoutChildren();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        RebuildParts();
        LayoutChildren();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        OnLayoutChanged();
    }

    /// <summary>按当前宽度与页数算出所有可画元素的位置。</summary>
    private void RebuildParts()
    {
        _parts.Clear();
        _totalRect = Rectangle.Empty;
        _comboRect = Rectangle.Empty;
        _compactActive = false;

        var inner = new Rectangle(
            Padding.Left + 1,
            Padding.Top + 1,
            Math.Max(0, Width - Padding.Horizontal - 2),
            Math.Max(0, Height - Padding.Vertical - 2));

        if (inner.Width <= 0 || inner.Height <= 0)
        {
            return;
        }

        var itemHeight = Math.Max(16, Math.Min(_itemHeight, inner.Height));
        var itemWidth = Math.Max(16, _itemWidth);
        var gap = Math.Max(0, _gap);
        var top = inner.Y + (inner.Height - itemHeight) / 2;

        // 右侧：每页条数下拉 → 总数文字（都由右往左占位）
        var right = inner.Right;

        if (_showPageSizeSelector && _pageSizeCombo is not null)
        {
            var comboWidth = Math.Min(120, Math.Max(88, itemWidth * 3));
            _comboRect = new Rectangle(right - comboWidth, top, comboWidth, itemHeight);
            right -= comboWidth + gap * 2;
        }

        if (_showTotal && _totalFormat.Length > 0)
        {
            var text = FormatTotal();
            var size = TextRenderer.MeasureText(
                text,
                Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            if (right - size.Width >= inner.Left)
            {
                _totalRect = new Rectangle(right - size.Width, top, size.Width, itemHeight);
                right -= size.Width + gap * 2;
            }
        }

        // 导航与页码都靠左连排成一整组，右侧留给总数与每页条数：
        // 先按「两侧导航会占多少」把页码可用宽度算出来，再从右往左判断页码放几个，避免溢出到总数文字上。
        var pageCount = PageCount;
        var leadingWidth = 0;
        if (_showFirstLast)
        {
            leadingWidth += itemWidth + gap;
        }

        if (_showPrevNext)
        {
            leadingWidth += itemWidth + gap;
        }

        var trailingWidth = 0;
        if (_showPrevNext)
        {
            trailingWidth += itemWidth + gap;
        }

        if (_showFirstLast)
        {
            trailingWidth += itemWidth + gap;
        }

        var pageLimit = Math.Max(inner.Left, right - trailingWidth);
        var available = Math.Max(0, pageLimit - inner.Left - leadingWidth);

        var compactText = string.Format(_pageTextFormat, _page, pageCount);
        var compactWidth = Math.Max(
            itemWidth * 2,
            TextRenderer.MeasureText(
                compactText,
                Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width + 20);

        var slots = itemWidth + gap > 0 ? available / (itemWidth + gap) : 0;
        _compactActive = _compact || (_autoCompact && slots < 5);

        var x = inner.Left;
        if (_showFirstLast)
        {
            _parts.Add(new Part(PartKind.First, -1, new Rectangle(x, top, itemWidth, itemHeight)));
            x += itemWidth + gap;
        }

        if (_showPrevNext)
        {
            _parts.Add(new Part(PartKind.Previous, -1, new Rectangle(x, top, itemWidth, itemHeight)));
            x += itemWidth + gap;
        }

        // 紧凑模式：‹ 「1 / 24」 ›
        if (_compactActive)
        {
            var labelWidth = Math.Min(compactWidth, Math.Max(0, pageLimit - x));
            if (labelWidth > 0)
            {
                _parts.Add(new Part(PartKind.CompactLabel, -1, new Rectangle(x, top, labelWidth, itemHeight)));
                x += labelWidth + gap;
            }
        }
        else
        {
            // 页码槽位：留两个位置给省略号
            var maxButtons = _maxPageButtons;
            if (slots > 0)
            {
                maxButtons = Math.Min(maxButtons, Math.Max(3, (int)slots - 2));
            }

            foreach (var page in BuildPageSequence(pageCount, _page, maxButtons))
            {
                var width = page < 0 ? Math.Max(20, itemWidth - gap) : itemWidth;
                if (x + width > pageLimit)
                {
                    break;
                }

                _parts.Add(new Part(page < 0 ? PartKind.Ellipsis : PartKind.Page, page, new Rectangle(x, top, width, itemHeight)));
                x += width + gap;
            }
        }

        if (_showPrevNext && x + itemWidth <= right)
        {
            _parts.Add(new Part(PartKind.Next, -1, new Rectangle(x, top, itemWidth, itemHeight)));
            x += itemWidth + gap;
        }

        if (_showFirstLast && x + itemWidth <= right)
        {
            _parts.Add(new Part(PartKind.Last, -1, new Rectangle(x, top, itemWidth, itemHeight)));
        }
    }

    /// <summary>
    /// 页码序列：首尾始终可见，中间按当前页开窗；只跳过一个数字时不画省略号（直接显示那个数字）。
    /// 负数（-1）表示省略号。
    /// </summary>
    internal static List<int> BuildPageSequence(int pageCount, int current, int maxButtons)
    {
        var result = new List<int>();
        if (pageCount <= 0)
        {
            result.Add(1);
            return result;
        }

        var limit = Math.Max(3, maxButtons);
        if (pageCount <= limit)
        {
            for (var i = 1; i <= pageCount; i++)
            {
                result.Add(i);
            }

            return result;
        }

        var window = Math.Max(1, limit - 2);                       // 去掉首尾后中间可放的数字
        var maxStart = Math.Max(2, pageCount - 1 - (window - 1));  // 中间窗口的最大起点
        var start = Math.Min(Math.Max(2, current - (window - 1) / 2), maxStart);
        var end = Math.Min(pageCount - 1, start + window - 1);

        result.Add(1);

        if (start == 2)
        {
            for (var i = 2; i <= end; i++)
            {
                result.Add(i);
            }
        }
        else
        {
            result.Add(-1);
            for (var i = start; i <= end; i++)
            {
                result.Add(i);
            }
        }

        if (end == pageCount - 1)
        {
            result.Add(pageCount);
        }
        else
        {
            result.Add(-1);
            result.Add(pageCount);
        }

        return result;
    }

    private string FormatTotal()
    {
        try
        {
            return string.Format(_totalFormat, _totalCount);
        }
        catch (FormatException)
        {
            return _totalCount.ToString();
        }
    }

    private bool IsPartEnabled(PartKind kind) => kind switch
    {
        PartKind.First or PartKind.Previous => _page > 1,
        PartKind.Next or PartKind.Last => _page < PageCount,
        _ => true,
    };

    private bool IsPartInteractive(PartKind kind) => kind switch
    {
        PartKind.Ellipsis or PartKind.CompactLabel => false,
        _ => true,
    };

    // ---------------- 每页条数下拉 ----------------

    private void AttachPageSizeCombo()
    {
        if (_showPageSizeSelector)
        {
            _pageSizeCombo ??= CreatePageSizeCombo();
            if (!Controls.Contains(_pageSizeCombo))
            {
                Controls.Add(_pageSizeCombo);
            }

            RebuildPageSizeItems();
        }
        else if (_pageSizeCombo is not null && Controls.Contains(_pageSizeCombo))
        {
            Controls.Remove(_pageSizeCombo);
        }
    }

    private SuComboBox CreatePageSizeCombo()
    {
        var combo = new SuComboBox { TabIndex = 0 };
        combo.SelectedIndexChanged += (_, __) =>
        {
            if (_syncingCombo)
            {
                return;
            }

            var index = combo.SelectedIndex;
            if (index < 0 || index >= _pageSizeOptions.Count)
            {
                return;
            }

            PageSize = _pageSizeOptions[index];
        };

        return combo;
    }

    private void RebuildPageSizeItems()
    {
        if (_pageSizeCombo is null)
        {
            return;
        }

        _syncingCombo = true;
        try
        {
            _pageSizeCombo.Items.Clear();
            foreach (var size in _pageSizeOptions)
            {
                _pageSizeCombo.Items.Add(FormatPageSize(size));
            }
        }
        finally
        {
            _syncingCombo = false;
        }

        SyncPageSizeCombo();
    }

    private string FormatPageSize(int size)
    {
        try
        {
            return string.Format(_pageSizeFormat, size);
        }
        catch (FormatException)
        {
            return size.ToString();
        }
    }

    private void SyncPageSizeCombo()
    {
        if (_pageSizeCombo is null)
        {
            return;
        }

        var index = _pageSizeOptions.IndexOf(_pageSize);
        if (index < 0 || index == _pageSizeCombo.SelectedIndex)
        {
            return;
        }

        _syncingCombo = true;
        try
        {
            _pageSizeCombo.SelectedIndex = index;
        }
        finally
        {
            _syncingCombo = false;
        }
    }

    private void LayoutChildren()
    {
        if (_pageSizeCombo is null || !Controls.Contains(_pageSizeCombo) || _comboRect.Width <= 0)
        {
            return;
        }

        _pageSizeCombo.SetBounds(_comboRect.X, _comboRect.Y, _comboRect.Width, _comboRect.Height);
    }

    // ---------------- 绘制 ----------------

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        SuDraw.SetupQuality(graphics);

        RebuildParts();
        LayoutChildren();

        using (var clear = new SolidBrush(BackColor))
        {
            graphics.FillRectangle(clear, ClientRectangle);
        }

        var theme = Theme;

        if (!_totalRect.IsEmpty)
        {
            TextRenderer.DrawText(
                graphics,
                FormatTotal(),
                Font,
                _totalRect,
                Enabled ? theme.TextSecondary : theme.TextDisabled,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }

        for (var i = 0; i < _parts.Count; i++)
        {
            DrawPart(graphics, theme, _parts[i], i);
        }
    }

    private void DrawPart(Graphics graphics, SuTheme theme, Part part, int index)
    {
        var rect = part.Rect;
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        var radius = _radius >= 0 ? _radius : theme.Radius;
        var interactive = Enabled && IsPartInteractive(part.Kind);
        var partEnabled = interactive && IsPartEnabled(part.Kind);
        var hovered = partEnabled && index == _hoverPart;
        var pressed = partEnabled && index == _pressedPart;
        var isCurrent = part.Kind == PartKind.Page && part.Page == _page;

        if (part.Kind == PartKind.Ellipsis)
        {
            DrawTextCentered(graphics, "···", rect, Enabled ? theme.TextSecondary : theme.TextDisabled);
            return;
        }

        Color fill;
        Color border;
        Color foreground;

        if (isCurrent)
        {
            fill = pressed ? theme.PrimaryPressed : hovered ? theme.PrimaryHover : theme.Primary;
            border = fill;
            foreground = theme.TextOnAccent;
        }
        else if (part.Kind == PartKind.CompactLabel)
        {
            fill = theme.SurfaceAlt;
            border = theme.Border;
            foreground = Enabled ? theme.TextPrimary : theme.TextDisabled;
        }
        else if (!partEnabled)
        {
            fill = theme.Surface;
            border = theme.Border;
            foreground = theme.TextDisabled;
        }
        else
        {
            var tint = hovered ? Math.Max(0.5f, _hoverTween.Value) : 0f;
            fill = pressed
                ? SuDraw.Blend(theme.Surface, theme.SurfaceAlt, 0.9f)
                : SuDraw.Blend(theme.Surface, theme.SurfaceAlt, tint * 0.75f);
            border = hovered ? theme.BorderStrong : theme.Border;
            foreground = theme.TextPrimary;
        }

        var shape = new RectangleF(rect.X + 0.5f, rect.Y + 0.5f, rect.Width - 1f, rect.Height - 1f);
        using (var path = SuDraw.RoundedRect(shape, radius))
        {
            using (var brush = new SolidBrush(fill))
            {
                graphics.FillPath(brush, path);
            }

            using (var pen = new Pen(border, 1f))
            {
                graphics.DrawPath(pen, path);
            }

            if (Focused && isCurrent)
            {
                var ring = RectangleF.Inflate(shape, -2f, -2f);
                using var ringPath = SuDraw.RoundedRect(ring, Math.Max(1, radius - 1));
                using var ringPen = new Pen(SuDraw.Alpha(theme.TextOnAccent, 160), 1f);
                graphics.DrawPath(ringPen, ringPath);
            }
        }

        switch (part.Kind)
        {
            case PartKind.Page:
                DrawTextCentered(graphics, part.Page.ToString(), rect, foreground);
                break;

            case PartKind.CompactLabel:
                DrawTextCentered(graphics, string.Format(_pageTextFormat, _page, PageCount), rect, foreground);
                break;

            case PartKind.First:
                DrawChevrons(graphics, rect, foreground, -1, true);
                break;

            case PartKind.Previous:
                DrawChevrons(graphics, rect, foreground, -1, false);
                break;

            case PartKind.Next:
                DrawChevrons(graphics, rect, foreground, 1, false);
                break;

            case PartKind.Last:
                DrawChevrons(graphics, rect, foreground, 1, true);
                break;
        }
    }

    private void DrawTextCentered(Graphics graphics, string text, Rectangle rect, Color color)
    {
        TextRenderer.DrawText(
            graphics,
            text,
            Font,
            rect,
            color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }

    /// <summary>画箭头：单个 chevron，<paramref name="doubleArrow"/> 为 true 时画两个（首页 / 末页）。</summary>
    private static void DrawChevrons(Graphics graphics, Rectangle rect, Color color, int direction, bool doubleArrow)
    {
        var centerY = rect.Top + rect.Height / 2f;
        var centerX = rect.Left + rect.Width / 2f;
        var size = Math.Max(3f, Math.Min(4.5f, rect.Height / 8f));

        using var pen = new Pen(color, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        for (var i = 0; i < (doubleArrow ? 2 : 1); i++)
        {
            var offset = doubleArrow ? (i == 0 ? -3f : 3f) : 0f;
            var x = centerX + offset * direction;

            graphics.DrawLines(
                pen,
                new[]
                {
                    new PointF(x - size * direction * 0.5f, centerY - size),
                    new PointF(x + size * direction * 0.5f, centerY),
                    new PointF(x - size * direction * 0.5f, centerY + size),
                });
        }
    }

    // ---------------- 命中测试与交互 ----------------

    private int HitPart(Point point)
    {
        RebuildParts();
        for (var i = 0; i < _parts.Count; i++)
        {
            if (_parts[i].Rect.Contains(point))
            {
                return i;
            }
        }

        return -1;
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var index = HitPart(e.Location);
        if (index != _hoverPart)
        {
            _hoverPart = index;
            _hoverTween.Snap(0f);
            _hoverTween.To(1f, 110);
            Invalidate();
        }

        var clickable = index >= 0 && IsPartInteractive(_parts[index].Kind) && IsPartEnabled(_parts[index].Kind);
        Cursor = clickable ? Cursors.Hand : Cursors.Default;
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        if (_hoverPart != -1 || _pressedPart != -1)
        {
            _hoverPart = -1;
            _pressedPart = -1;
            _hoverTween.To(0f, 140);
            Cursor = Cursors.Default;
            Invalidate();
        }

        base.OnMouseLeave(e);
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        var index = HitPart(e.Location);
        if (index < 0)
        {
            return;
        }

        var part = _parts[index];
        if (!IsPartInteractive(part.Kind) || !IsPartEnabled(part.Kind))
        {
            return;
        }

        _pressedPart = index;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (_pressedPart < 0)
        {
            return;
        }

        var pressed = _pressedPart;
        _pressedPart = -1;
        Invalidate();

        var index = HitPart(e.Location);
        if (index != pressed)
        {
            return;
        }

        Activate(_parts[index]);
    }

    private void Activate(Part part)
    {
        switch (part.Kind)
        {
            case PartKind.First:
                FirstPage();
                break;

            case PartKind.Previous:
                PreviousPage();
                break;

            case PartKind.Next:
                NextPage();
                break;

            case PartKind.Last:
                LastPage();
                break;

            case PartKind.Page when part.Page > 0:
                GoToPage(part.Page);
                break;
        }
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
    protected override bool IsInputKey(Keys keyData)
    {
        switch (keyData & Keys.KeyCode)
        {
            case Keys.Left:
            case Keys.Right:
            case Keys.Home:
            case Keys.End:
            case Keys.PageUp:
            case Keys.PageDown:
                return true;
        }

        return base.IsInputKey(keyData);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        switch (e.KeyCode)
        {
            case Keys.Left:
            case Keys.PageUp:
                PreviousPage();
                e.Handled = true;
                break;

            case Keys.Right:
            case Keys.PageDown:
                NextPage();
                e.Handled = true;
                break;

            case Keys.Home:
                FirstPage();
                e.Handled = true;
                break;

            case Keys.End:
                LastPage();
                e.Handled = true;
                break;
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hoverTween.Dispose();
        }

        base.Dispose(disposing);
    }
}

/// <summary>分页事件参数。</summary>
public class SuPageChangedEventArgs : EventArgs
{
    public SuPageChangedEventArgs(int oldPage, int newPage, int pageCount, int totalCount, bool byUser)
    {
        OldPage = oldPage;
        NewPage = newPage;
        PageCount = pageCount;
        TotalCount = totalCount;
        ByUser = byUser;
    }

    /// <summary>变化前的页号。</summary>
    public int OldPage { get; }

    /// <summary>变化后的页号。</summary>
    public int NewPage { get; }

    /// <summary>总页数。</summary>
    public int PageCount { get; }

    /// <summary>总条数。</summary>
    public int TotalCount { get; }

    /// <summary>是否由用户操作（点击 / 键盘）触发；程序设值或数据变化收页时为 false。</summary>
    public bool ByUser { get; }
}