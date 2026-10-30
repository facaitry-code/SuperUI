using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using SuperUI.Rendering;
using SuperUI.Theming;

namespace SuperUI.Controls;


/// <summary>
/// SuperUI 侧边导航菜单：纵向条目 + 分组标题 + 分隔线 + 多级子项（可展开/收起）+ 徽标，
/// 选中项用主色浅底 + 左侧强调条标识，超出高度时自带细滚动条并支持滚轮 / 键盘导航。
/// 颜色全部取自 <see cref="SuTheme"/>，随全局主题即时切换。
/// </summary>
[Description("SuperUI 侧边菜单：分组、子项展开、选中高亮、徽标、滚动与键盘导航，颜色跟随全局主题。")]
[DefaultEvent(nameof(SelectedItemChanged))]
[DefaultProperty(nameof(Items))]
public class SuSideMenu : SuControlBase
{
    private readonly SuTween _hoverTween;
    private readonly List<Entry> _entries = new();
    private readonly ObservableCollection<SuMenuItem> _items = new();
    private SuMenuItem? _hovered;
    private SuMenuItem? _selected;
    private Font? _groupFont;
    private Font? _glyphFont;
    private int _scrollOffset;
    private int _contentHeight;
    private bool _scrollBarVisible;
    private int _itemHeight = 40;
    private int _groupHeaderHeight = 30;
    private int _separatorHeight = 13;
    private int _indent = 16;
    private Color? _panelColor;
    private string _iconFontName = "Segoe MDL2 Assets";
    private bool _showRightBorder = true;

    public SuSideMenu()
    {
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        Padding = new Padding(8, 8, 8, 8);

        _hoverTween = new SuTween();
        _hoverTween.Changed += (_, __) => Invalidate();

        _items.CollectionChanged += ItemsOnCollectionChanged;

        ApplyTheme();
    }

    /// <summary>点击某个可点击项（已选中之后触发）。</summary>
    public event EventHandler<SuMenuItemEventArgs>? ItemClick;

    /// <summary>选中项变化。</summary>
    public event EventHandler? SelectedItemChanged;

    /// <summary>顶菜单项集合（子项挂在 <see cref="SuMenuItem.Children"/> 上）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public ObservableCollection<SuMenuItem> Items => _items;

    /// <summary>选中项。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SuMenuItem? SelectedItem
    {
        get => _selected;
        set
        {
            if (ReferenceEquals(_selected, value))
            {
                return;
            }

            _selected = value;
            if (value is not null)
            {
                value.ExpandAncestors();
                EnsureVisible(value);
            }

            SelectedItemChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    /// <summary>选中项的 Key（没有选中时为 null）。</summary>
    [Browsable(false)]
    public string? SelectedKey => _selected?.Key;

    /// <summary>单个条目高度。</summary>
    [Category("SuperUI")]
    [DefaultValue(40)]
    public int ItemHeight
    {
        get => _itemHeight;
        set
        {
            var height = Math.Max(20, value);
            if (_itemHeight == height)
            {
                return;
            }

            _itemHeight = height;
            UpdateScrollBounds();
            Invalidate();
        }
    }

    /// <summary>分组标题高度。</summary>
    [Category("SuperUI")]
    [DefaultValue(30)]
    public int GroupHeaderHeight
    {
        get => _groupHeaderHeight;
        set
        {
            var height = Math.Max(16, value);
            if (_groupHeaderHeight == height)
            {
                return;
            }

            _groupHeaderHeight = height;
            UpdateScrollBounds();
            Invalidate();
        }
    }

    /// <summary>分隔线高度。</summary>
    [Category("SuperUI")]
    [DefaultValue(13)]
    public int SeparatorHeight
    {
        get => _separatorHeight;
        set
        {
            var height = Math.Max(6, value);
            if (_separatorHeight == height)
            {
                return;
            }

            _separatorHeight = height;
            UpdateScrollBounds();
            Invalidate();
        }
    }

    /// <summary>每级子项的缩进宽度。</summary>
    [Category("SuperUI")]
    [DefaultValue(16)]
    public int Indent
    {
        get => _indent;
        set
        {
            var indent = Math.Max(0, value);
            if (_indent == indent)
            {
                return;
            }

            _indent = indent;
            Invalidate();
        }
    }

    /// <summary>菜单底色；为 null 时用主题的 Surface。</summary>
    [Category("SuperUI")]
    [Description("菜单底色。null 表示使用主题的 Surface 色。")]
    public Color? PanelColor
    {
        get => _panelColor;
        set
        {
            if (_panelColor == value)
            {
                return;
            }

            _panelColor = value;
            Invalidate();
        }
    }

    /// <summary>字形图标所用的字体（配合 <see cref="SuMenuItem.Glyph"/>）。</summary>
    [Category("SuperUI")]
    [DefaultValue("Segoe MDL2 Assets")]
    public string IconFont
    {
        get => _iconFontName;
        set
        {
            value = string.IsNullOrWhiteSpace(value) ? "Segoe MDL2 Assets" : value;
            if (_iconFontName == value)
            {
                return;
            }

            _iconFontName = value;
            RebuildFonts();
            Invalidate();
        }
    }

    /// <summary>是否在右侧画 1 像素分隔线。</summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    public bool ShowRightBorder
    {
        get => _showRightBorder;
        set
        {
            if (_showRightBorder == value)
            {
                return;
            }

            _showRightBorder = value;
            Invalidate();
        }
    }

    /// <summary>展开全部含子项的条目。</summary>
    public void ExpandAll()
    {
        SetExpandedRecursive(_items, true);
        UpdateScrollBounds();
        Invalidate();
    }

    /// <summary>收起全部含子项的条目。</summary>
    public void CollapseAll()
    {
        SetExpandedRecursive(_items, false);
        UpdateScrollBounds();
        Invalidate();
    }

    /// <summary>按 Key 选中。</summary>
    public bool SelectByKey(string key)
    {
        var item = FindByKey(_items, key);
        if (item is null)
        {
            return false;
        }

        SelectedItem = item;
        return true;
    }

    /// <summary>追加顶级菜单项。</summary>
    public SuMenuItem Add(SuMenuItem item)
    {
        _items.Add(item);
        return item;
    }

    /// <summary>追加顶级菜单项（按文本）。</summary>
    public SuMenuItem Add(string text) => Add(new SuMenuItem(text));

    /// <summary>追加分组标题。</summary>
    public SuMenuItem AddGroup(string text) => Add(SuMenuItem.Group(text));

    /// <summary>追加分隔线。</summary>
    public SuMenuItem AddSeparator() => Add(SuMenuItem.Separator());

    /// <inheritdoc />
    public override void OnThemeChanged()
    {
        ApplyTheme();
        UpdateScrollBounds();
        Invalidate(true);
    }

    /// <inheritdoc />
    protected override void ApplyTheme()
    {
        base.ApplyTheme();
        RebuildFonts();
    }

    private void RebuildFonts()
    {
        if (Font is null)
        {
            return;
        }

        var groupFont = CreateFontSafe(Font.FontFamily.Name, Math.Max(7f, Font.Size - 1f));
        var glyphFont = CreateFontSafe(_iconFontName, Math.Max(8f, Font.Size + 1f));

        var previousGroup = _groupFont;
        var previousGlyph = _glyphFont;
        _groupFont = groupFont;
        _glyphFont = glyphFont;

        previousGroup?.Dispose();
        if (!ReferenceEquals(previousGlyph, glyphFont))
        {
            previousGlyph?.Dispose();
        }
    }

    private static Font CreateFontSafe(string family, float size)
    {
        try
        {
            return new Font(family, size, FontStyle.Regular, GraphicsUnit.Point);
        }
        catch (ArgumentException)
        {
            return new Font(FontFamily.GenericSansSerif, size, FontStyle.Regular, GraphicsUnit.Point);
        }
    }

    private void ItemsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (SuMenuItem item in e.OldItems)
            {
                item.Changed -= ItemOnChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (SuMenuItem item in e.NewItems)
            {
                item.Changed += ItemOnChanged;
            }
        }

        UpdateScrollBounds();
        Invalidate();
    }

    private void ItemOnChanged(object? sender, EventArgs e)
    {
        UpdateScrollBounds();
        Invalidate();
    }

    // ---------------- 布局与命中测试 ----------------

    private readonly struct Entry
    {
        public Entry(SuMenuItem item, int level, int top, int height)
        {
            Item = item;
            Level = level;
            Top = top;
            Height = height;
        }

        public SuMenuItem Item { get; }

        public int Level { get; }

        public int Top { get; }

        public int Height { get; }
    }

    /// <summary>按当前展开状态与滚动偏移重算每个条目的位置。</summary>
    private void RebuildEntries()
    {
        _entries.Clear();

        var top = Padding.Top - _scrollOffset;
        Walk(_items, 0, ref top);

        _contentHeight = top + _scrollOffset + Padding.Bottom;
        _scrollBarVisible = _contentHeight > ClientSize.Height;
    }

    private void Walk(IEnumerable<SuMenuItem> items, int level, ref int top)
    {
        foreach (var item in items)
        {
            var height = item.Kind switch
            {
                SuMenuItemKind.Separator => _separatorHeight,
                SuMenuItemKind.Group => _groupHeaderHeight,
                _ => _itemHeight,
            };

            _entries.Add(new Entry(item, level, top, height));
            top += height;

            if (item.HasChildren && item.IsExpanded)
            {
                Walk(item.Children, level + 1, ref top);
            }
        }
    }

    private Rectangle GetItemRect(Entry entry)
    {
        var left = Padding.Left + entry.Level * _indent;
        var reserved = _scrollBarVisible ? 8 : 0;
        var width = Math.Max(0, ClientSize.Width - Padding.Horizontal - reserved - entry.Level * _indent);
        return new Rectangle(left, entry.Top + 1, width, Math.Max(0, entry.Height - 2));
    }

    /// <summary>命中测试：返回鼠标下的条目。</summary>
    private SuMenuItem? HitTest(Point point)
    {
        RebuildEntries();
        foreach (var entry in _entries)
        {
            if (point.Y >= entry.Top && point.Y < entry.Top + entry.Height)
            {
                return entry.Item;
            }
        }

        return null;
    }

    private void UpdateScrollBounds()
    {
        RebuildEntries();
        var max = Math.Max(0, _contentHeight - ClientSize.Height);
        if (_scrollOffset > max)
        {
            _scrollOffset = max;
            RebuildEntries();
        }
    }

    private void ScrollBy(int delta)
    {
        var max = Math.Max(0, _contentHeight - ClientSize.Height);
        var target = SuMath.Clamp(_scrollOffset + delta, 0, max);
        if (target == _scrollOffset)
        {
            return;
        }

        _scrollOffset = target;
        RebuildEntries();
        Invalidate();
    }

    private void EnsureVisible(SuMenuItem item)
    {
        RebuildEntries();
        var found = false;
        var entry = default(Entry);

        foreach (var candidate in _entries)
        {
            if (ReferenceEquals(candidate.Item, item))
            {
                entry = candidate;
                found = true;
                break;
            }
        }

        if (!found)
        {
            return;
        }

        if (entry.Top < Padding.Top)
        {
            ScrollBy(entry.Top - Padding.Top);
        }
        else if (entry.Top + entry.Height > ClientSize.Height - Padding.Bottom)
        {
            ScrollBy(entry.Top + entry.Height - (ClientSize.Height - Padding.Bottom));
        }
    }

    // ---------------- 绘制 ----------------

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        SuDraw.SetupQuality(graphics);

        var theme = Theme;
        var panel = _panelColor ?? theme.Surface;

        RebuildEntries();

        using (var clear = new SolidBrush(BackColor))
        {
            graphics.FillRectangle(clear, ClientRectangle);
        }

        using (var fill = new SolidBrush(panel))
        {
            graphics.FillRectangle(fill, ClientRectangle);
        }

        var radius = Math.Max(2, theme.Radius);
        foreach (var entry in _entries)
        {
            if (entry.Top + entry.Height < 0 || entry.Top > Height)
            {
                continue;
            }

            DrawEntry(graphics, theme, entry, panel, radius);
        }

        DrawScrollBar(graphics, theme);

        if (_showRightBorder)
        {
            using var borderPen = new Pen(theme.Border);
            graphics.DrawLine(borderPen, Width - 1, 0, Width - 1, Height);
        }
    }

    private void DrawEntry(Graphics graphics, SuTheme theme, Entry entry, Color panel, int radius)
    {
        var item = entry.Item;
        var rect = GetItemRect(entry);

        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        if (item.Kind == SuMenuItemKind.Separator)
        {
            using var pen = new Pen(theme.Border);
            var y = entry.Top + entry.Height / 2f;
            graphics.DrawLine(pen, rect.Left + 6f, y, rect.Right - 6f, y);
            return;
        }

        if (item.Kind == SuMenuItemKind.Group)
        {
            var groupFont = _groupFont ?? Font;
            var bounds = new Rectangle(rect.Left + 8, entry.Top, Math.Max(0, rect.Width - 12), entry.Height);
            TextRenderer.DrawText(
                graphics,
                item.Text,
                groupFont,
                bounds,
                theme.TextSecondary,
                TextFormatFlags.Left
                | TextFormatFlags.VerticalCenter
                | TextFormatFlags.EndEllipsis
                | TextFormatFlags.NoPadding
                | TextFormatFlags.NoPrefix);
            return;
        }

        var selected = ReferenceEquals(_selected, item);
        var hovered = ReferenceEquals(_hovered, item);
        var enabled = item.Enabled;

        if (selected)
        {
            using var path = SuDraw.RoundedRect(rect, radius);
            using (var brush = new SolidBrush(theme.PrimarySoft))
            {
                graphics.FillPath(brush, path);
            }

            var barRect = new RectangleF(rect.Left + 2f, rect.Top + rect.Height / 2f - 9f, 3f, 18f);
            using var barPath = SuDraw.RoundedRect(barRect, 1.5f);
            using var barBrush = new SolidBrush(theme.Primary);
            graphics.FillPath(barBrush, barPath);
        }
        else if (hovered && enabled)
        {
            using var path = SuDraw.RoundedRect(rect, radius);
            using var brush = new SolidBrush(SuDraw.Blend(panel, theme.SurfaceAlt, Math.Max(0.35f, _hoverTween.Value)));
            graphics.FillPath(brush, path);
        }

        if (Focused && selected)
        {
            var inner = RectangleF.Inflate(rect, -2f, -2f);
            using var innerPath = SuDraw.RoundedRect(inner, Math.Max(0, radius - 2));
            using var focusPen = new Pen(SuDraw.Alpha(theme.Primary, 120), 1f);
            graphics.DrawPath(focusPen, innerPath);
        }

        var foreground = !enabled
            ? theme.TextDisabled
            : selected
                ? theme.Primary
                : theme.TextPrimary;

        var left = rect.Left + 12;
        var iconSize = Math.Max(14, Font.Height + 2);

        if (item.Icon is not null)
        {
            var iconRect = new Rectangle(left, rect.Top + (rect.Height - iconSize) / 2, iconSize, iconSize);
            graphics.DrawImage(item.Icon, iconRect);
            left += iconSize + 10;
        }
        else if (!string.IsNullOrEmpty(item.Glyph) && _glyphFont is not null)
        {
            var iconRect = new Rectangle(left, rect.Top, iconSize + 4, rect.Height);
            TextRenderer.DrawText(
                graphics,
                item.Glyph,
                _glyphFont,
                iconRect,
                foreground,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            left += iconSize + 10;
        }

        var right = rect.Right - 12;

        if (item.HasChildren)
        {
            DrawChevron(
                graphics,
                new PointF(right - 5f, rect.Top + rect.Height / 2f),
                4f,
                item.IsExpanded,
                foreground);
            right -= 18;
        }

        if (!string.IsNullOrEmpty(item.Badge))
        {
            var badgeSize = TextRenderer.MeasureText(
                graphics,
                item.Badge,
                _groupFont ?? Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            var badgeRect = new Rectangle(
                right - badgeSize.Width - 10,
                rect.Top + (rect.Height - 18) / 2,
                badgeSize.Width + 10,
                18);

            if (badgeRect.Left > left)
            {
                using var badgePath = SuDraw.RoundedRect(badgeRect, 9f);
                using var badgeBrush = new SolidBrush(selected ? theme.Primary : theme.PrimarySoft);
                graphics.FillPath(badgeBrush, badgePath);

                TextRenderer.DrawText(
                    graphics,
                    item.Badge,
                    _groupFont ?? Font,
                    badgeRect,
                    selected ? theme.TextOnAccent : theme.Primary,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

                right = badgeRect.Left - 8;
            }
        }

        var textRect = new Rectangle(left, rect.Top, Math.Max(0, right - left), rect.Height);
        if (textRect.Width > 0)
        {
            TextRenderer.DrawText(
                graphics,
                item.Text,
                Font,
                textRect,
                foreground,
                TextFormatFlags.Left
                | TextFormatFlags.VerticalCenter
                | TextFormatFlags.EndEllipsis
                | TextFormatFlags.NoPadding
                | TextFormatFlags.NoPrefix);
        }
    }

    private static void DrawChevron(Graphics graphics, PointF center, float size, bool expanded, Color color)
    {
        using var pen = new Pen(color, 1.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        if (expanded)
        {
            graphics.DrawLines(
                pen,
                new[]
                {
                    new PointF(center.X - size, center.Y - size / 2f),
                    new PointF(center.X, center.Y + size / 2f),
                    new PointF(center.X + size, center.Y - size / 2f),
                });
        }
        else
        {
            graphics.DrawLines(
                pen,
                new[]
                {
                    new PointF(center.X - size / 2f, center.Y - size),
                    new PointF(center.X + size / 2f, center.Y),
                    new PointF(center.X - size / 2f, center.Y + size),
                });
        }
    }

    private void DrawScrollBar(Graphics graphics, SuTheme theme)
    {
        if (!_scrollBarVisible || _contentHeight <= 0)
        {
            return;
        }

        var trackHeight = Math.Max(1, ClientSize.Height - Padding.Vertical);
        var ratio = ClientSize.Height / (double)_contentHeight;
        var thumbHeight = Math.Max(26, (int)(trackHeight * ratio));
        var maxScroll = Math.Max(1, _contentHeight - ClientSize.Height);
        var offset = (trackHeight - thumbHeight) * (_scrollOffset / (double)maxScroll);

        var rect = new RectangleF(Width - 8f, Padding.Top + (float)offset, 4f, thumbHeight);
        using var path = SuDraw.RoundedRect(rect, 2f);
        using var brush = new SolidBrush(SuDraw.Alpha(theme.BorderStrong, 210));
        graphics.FillPath(brush, path);
    }

    // ---------------- 交互 ----------------

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        var item = HitTest(e.Location);
        if (!ReferenceEquals(item, _hovered))
        {
            _hovered = item;
            _hoverTween.Snap(0f);
            _hoverTween.To(1f, 110);
            Cursor = item is not null && item.Kind == SuMenuItemKind.Item && item.Enabled
                ? Cursors.Hand
                : Cursors.Default;
            Invalidate();
        }

        base.OnMouseMove(e);
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        if (_hovered is not null)
        {
            _hovered = null;
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

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        Focus();

        var item = HitTest(e.Location);
        if (item is null || item.Kind != SuMenuItemKind.Item || !item.Enabled)
        {
            return;
        }

        if (item.HasChildren)
        {
            item.IsExpanded = !item.IsExpanded;
            UpdateScrollBounds();
            Invalidate();
            return;
        }

        SelectedItem = item;
        ItemClick?.Invoke(this, new SuMenuItemEventArgs(item));
    }

    /// <inheritdoc />
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        ScrollBy(-Math.Sign(e.Delta) * _itemHeight * 2);
    }

    /// <inheritdoc />
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateScrollBounds();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        RebuildFonts();
        UpdateScrollBounds();
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
    protected override bool IsInputKey(Keys keyData)
    {
        if (keyData is Keys.Up or Keys.Down or Keys.Left or Keys.Right)
        {
            return true;
        }

        return base.IsInputKey(keyData);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        var selectable = GetSelectableItems();

        switch (e.KeyCode)
        {
            case Keys.Down:
                MoveSelection(selectable, 1);
                e.Handled = true;
                break;

            case Keys.Up:
                MoveSelection(selectable, -1);
                e.Handled = true;
                break;

            case Keys.Home when selectable.Count > 0:
                SelectedItem = selectable[0];
                e.Handled = true;
                break;

            case Keys.End when selectable.Count > 0:
                SelectedItem = selectable[selectable.Count - 1];
                e.Handled = true;
                break;

            case Keys.Right when _selected is not null && _selected.HasChildren:
                _selected.IsExpanded = true;
                UpdateScrollBounds();
                Invalidate();
                e.Handled = true;
                break;

            case Keys.Left when _selected is not null && _selected.HasChildren && _selected.IsExpanded:
                _selected.IsExpanded = false;
                UpdateScrollBounds();
                Invalidate();
                e.Handled = true;
                break;

            case Keys.Left when _selected?.Parent is not null:
                SelectedItem = _selected.Parent;
                e.Handled = true;
                break;

            case Keys.Enter:
            case Keys.Space:
                if (_selected is not null)
                {
                    ItemClick?.Invoke(this, new SuMenuItemEventArgs(_selected));
                    e.Handled = true;
                }

                break;
        }
    }

    private void MoveSelection(List<SuMenuItem> selectable, int delta)
    {
        if (selectable.Count == 0)
        {
            return;
        }

        var index = _selected is null ? -1 : selectable.IndexOf(_selected);
        index += delta;

        if (index < 0)
        {
            index = 0;
        }
        else if (index >= selectable.Count)
        {
            index = selectable.Count - 1;
        }

        SelectedItem = selectable[index];
    }

    /// <summary>当前可见（父项已展开）的可点击条目。</summary>
    private List<SuMenuItem> GetSelectableItems()
    {
        var result = new List<SuMenuItem>();
        CollectSelectable(_items, result);
        return result;
    }

    private static void CollectSelectable(IEnumerable<SuMenuItem> items, List<SuMenuItem> result)
    {
        foreach (var item in items)
        {
            if (item.Kind == SuMenuItemKind.Item && item.Enabled)
            {
                result.Add(item);
            }

            if (item.HasChildren && item.IsExpanded)
            {
                CollectSelectable(item.Children, result);
            }
        }
    }

    private static void SetExpandedRecursive(IEnumerable<SuMenuItem> items, bool expanded)
    {
        foreach (var item in items)
        {
            if (item.HasChildren)
            {
                item.IsExpanded = expanded;
                SetExpandedRecursive(item.Children, expanded);
            }
        }
    }

    private static SuMenuItem? FindByKey(IEnumerable<SuMenuItem> items, string key)
    {
        foreach (var item in items)
        {
            if (string.Equals(item.Key, key, StringComparison.Ordinal))
            {
                return item;
            }

            var found = FindByKey(item.Children, key);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hoverTween.Dispose();
            _items.CollectionChanged -= ItemsOnCollectionChanged;
            foreach (var item in _items)
            {
                item.Changed -= ItemOnChanged;
            }

            _groupFont?.Dispose();
            if (!ReferenceEquals(_glyphFont, _groupFont))
            {
                _glyphFont?.Dispose();
            }
        }

        base.Dispose(disposing);
    }

}

/// <summary><see cref="SuSideMenu.ItemClick"/> 事件参数。</summary>
public class SuMenuItemEventArgs : EventArgs
{
    public SuMenuItemEventArgs(SuMenuItem item) => Item = item;

    /// <summary>被点击的菜单项。</summary>
    public SuMenuItem Item { get; }
}
