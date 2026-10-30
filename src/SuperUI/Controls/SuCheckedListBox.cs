using System.ComponentModel;
using System.Drawing.Drawing2D;
using SuperUI.Rendering;
using SuperUI.Theming;

namespace SuperUI.Controls;

/// <summary>
/// SuperUI 带复选框的列表框：圆角外框 + 内部自绘条目。
/// 复选框由本控件自己维护并绘制（不依赖原生 CheckedListBox 的绘制模式），
/// 因此浅色 / 深色主题下观感完全一致。
/// </summary>
[Description("SuperUI 带复选框的列表框：圆角外框 + 自绘复选条目与选中/悬停配色，颜色跟随全局主题。")]
[DefaultProperty(nameof(Items))]
[DefaultEvent(nameof(SelectedIndexChanged))]
public class SuCheckedListBox : SuControlBase
{
    private readonly ListBox _inner;
    private readonly List<int> _checked = new();
    private int _radius = -1;
    private int _hoverIndex = -1;
    private bool _innerHovered;
    private bool _checkOnClick = true;
    private int _itemHeight = 28;

    public SuCheckedListBox()
    {
        Padding = new Padding(6, 6, 6, 6);

        _inner = new ListBox
        {
            BorderStyle = BorderStyle.None,
            IntegralHeight = false,
            TabIndex = 0,
        };

        // 绘制模式在句柄创建之后再设置，避免被控件的初始化流程重置。
        _inner.DrawMode = DrawMode.OwnerDrawFixed;
        _inner.ItemHeight = _itemHeight;
        _inner.SelectionMode = SelectionMode.One;

        Controls.Add(_inner);

        _inner.DrawItem += InnerOnDrawItem;
        _inner.MouseMove += InnerOnMouseMove;
        _inner.MouseDown += InnerOnMouseDown;
        _inner.MouseLeave += (_, __) =>
        {
            _innerHovered = false;
            if (_hoverIndex != -1)
            {
                _hoverIndex = -1;
                _inner.Invalidate();
            }

            Invalidate();
        };
        _inner.SelectedIndexChanged += (_, e) =>
        {
            SelectedIndexChanged?.Invoke(this, e);
            _inner.Invalidate();
        };
        _inner.Enter += (_, __) => Invalidate();
        _inner.Leave += (_, __) => Invalidate();

        ApplyTheme();
    }

    /// <summary>某个条目的勾选状态发生变化。</summary>
    public event ItemCheckEventHandler? ItemCheck;

    /// <summary>选中项变化。</summary>
    public event EventHandler? SelectedIndexChanged;

    /// <summary>条目集合。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public ListBox.ObjectCollection Items => _inner.Items;

    /// <summary>已勾选的索引（升序）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<int> CheckedIndices => _checked;

    /// <summary>点击条目任意位置是否切换勾选（false 时只有点在复选框上才切换）。</summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    public bool CheckOnClick
    {
        get => _checkOnClick;
        set => _checkOnClick = value;
    }

    /// <summary>条目高度。</summary>
    [Category("SuperUI")]
    [DefaultValue(28)]
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
            _inner.ItemHeight = height;
            _inner.Invalidate();
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

    /// <summary>选中项索引。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => _inner.SelectedIndex;
        set => _inner.SelectedIndex = value;
    }

    /// <summary>内部原生列表框（高级用法）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ListBox InnerList => _inner;

    /// <summary>获取某个条目是否勾选。</summary>
    public bool GetItemChecked(int index)
        => index >= 0 && index < _inner.Items.Count && _checked.Contains(index);

    /// <summary>设置某个条目勾选状态（会触发 <see cref="ItemCheck"/>）。</summary>
    public void SetItemChecked(int index, bool value)
    {
        if (index < 0 || index >= _inner.Items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var isChecked = _checked.Contains(index);
        if (isChecked == value)
        {
            return;
        }

        var args = new ItemCheckEventArgs(
            index,
            value ? CheckState.Checked : CheckState.Unchecked,
            isChecked ? CheckState.Checked : CheckState.Unchecked);

        ItemCheck?.Invoke(this, args);

        var final = args.NewValue == CheckState.Checked;
        if (final == isChecked)
        {
            return;
        }

        if (final)
        {
            _checked.Add(index);
            _checked.Sort();
        }
        else
        {
            _checked.Remove(index);
        }

        _inner.Invalidate();
    }

    /// <summary>切换某个条目的勾选状态。</summary>
    public void ToggleItem(int index) => SetItemChecked(index, !GetItemChecked(index));

    /// <summary>全部勾选。</summary>
    public void CheckAll()
    {
        for (var i = 0; i < _inner.Items.Count; i++)
        {
            SetItemChecked(i, true);
        }
    }

    /// <summary>全部取消勾选。</summary>
    public void UncheckAll()
    {
        for (var i = _checked.Count - 1; i >= 0; i--)
        {
            SetItemChecked(_checked[i], false);
        }
    }

    /// <summary>清空并批量填充条目（勾选状态一并重置）。</summary>
    public void SetItems(params string[] items)
    {
        _checked.Clear();
        Items.Clear();

        if (items is null || items.Length == 0)
        {
            return;
        }

        Items.AddRange(items);
    }

    /// <inheritdoc />
    public override void OnThemeChanged()
    {
        ApplyTheme();
        Invalidate(true);
    }

    /// <inheritdoc />
    protected override void ApplyTheme()
    {
        base.ApplyTheme();

        var theme = Theme;
        _inner.BackColor = theme.Surface;
        _inner.ForeColor = theme.TextPrimary;
        _inner.Invalidate();
        LayoutInner();
    }

    private void LayoutInner()
    {
        if (_inner is null)
        {
            return;
        }

        _inner.SetBounds(
            Padding.Left,
            Padding.Top,
            Math.Max(0, ClientSize.Width - Padding.Horizontal),
            Math.Max(0, ClientSize.Height - Padding.Vertical));
    }

    /// <inheritdoc />
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutInner();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        SuDraw.SetupQuality(graphics);

        var theme = Theme;
        using (var clear = new SolidBrush(BackColor))
        {
            graphics.FillRectangle(clear, ClientRectangle);
        }

        var radius = _radius >= 0 ? _radius : theme.Radius;
        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        var surface = Enabled ? theme.Surface : theme.SurfaceAlt;
        var border = _inner.Focused
            ? theme.Primary
            : _innerHovered
                ? theme.BorderStrong
                : theme.Border;

        using var path = SuDraw.RoundedRect(rect, radius);
        using (var brush = new SolidBrush(surface))
        {
            graphics.FillPath(brush, path);
        }

        using (var pen = new Pen(border, _inner.Focused ? 1.4f : 1f))
        {
            graphics.DrawPath(pen, path);
        }
    }

    private void InnerOnMouseMove(object? sender, MouseEventArgs e)
    {
        _innerHovered = true;

        var index = _inner.IndexFromPoint(e.Location);
        if (index != _hoverIndex)
        {
            _hoverIndex = index;
            _inner.Invalidate();
        }
    }

    private void InnerOnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        var index = _inner.IndexFromPoint(e.Location);
        if (index < 0)
        {
            return;
        }

        var checkboxHit = e.X <= 30;
        if (_checkOnClick || checkboxHit)
        {
            ToggleItem(index);
        }
    }

    private void InnerOnDrawItem(object? sender, DrawItemEventArgs e)
    {
        var theme = Theme;
        var graphics = e.Graphics;

        if (e.Index < 0 || e.Index >= _inner.Items.Count)
        {
            return;
        }

        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var hovered = e.Index == _hoverIndex;
        var background = selected
            ? theme.PrimarySoft
            : hovered
                ? theme.SurfaceAlt
                : theme.Surface;

        using (var brush = new SolidBrush(background))
        {
            graphics.FillRectangle(brush, e.Bounds);
        }

        var isChecked = GetItemChecked(e.Index);
        const int boxSize = 16;

        var box = new RectangleF(
            e.Bounds.Left + 8,
            e.Bounds.Top + (e.Bounds.Height - boxSize) / 2f,
            boxSize,
            boxSize);

        using (var path = SuDraw.RoundedRect(box, 4f))
        {
            using (var brush = new SolidBrush(isChecked ? theme.Primary : theme.Surface))
            {
                graphics.FillPath(brush, path);
            }

            using (var pen = new Pen(
                       isChecked ? theme.Primary : SuDraw.Blend(theme.Border, theme.TextSecondary, 0.35f),
                       1.2f))
            {
                graphics.DrawPath(pen, path);
            }
        }

        if (isChecked)
        {
            var points = new[]
            {
                new PointF(box.Left + box.Width * 0.24f, box.Top + box.Height * 0.52f),
                new PointF(box.Left + box.Width * 0.43f, box.Top + box.Height * 0.72f),
                new PointF(box.Left + box.Width * 0.77f, box.Top + box.Height * 0.3f),
            };

            using var markPen = new Pen(theme.TextOnAccent, 1.8f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round,
            };

            graphics.DrawLines(markPen, points);
        }

        var textRect = new Rectangle(
            e.Bounds.Left + boxSize + 20,
            e.Bounds.Top,
            Math.Max(0, e.Bounds.Width - boxSize - 26),
            e.Bounds.Height);

        TextRenderer.DrawText(
            graphics,
            _inner.GetItemText(_inner.Items[e.Index]),
            Font,
            textRect,
            selected ? theme.Primary : theme.TextPrimary,
            TextFormatFlags.Left
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis
            | TextFormatFlags.NoPadding
            | TextFormatFlags.NoPrefix);
    }
}
