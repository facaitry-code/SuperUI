using System.ComponentModel;
using System.Drawing.Drawing2D;
using SuperUI.Rendering;
using SuperUI.Theming;

namespace SuperUI.Controls;

/// <summary>
/// SuperUI 下拉框：完全自绘的字段（圆角、悬停/聚焦描边、占位符、下拉箭头），
/// 下拉列表项也按主题绘制选中/悬停配色。
/// </summary>
[Description("SuperUI 下拉框：自绘字段 + 主题化下拉项，颜色跟随全局主题。")]
[DefaultProperty(nameof(Items))]
public class SuComboBox : ComboBox, ISuThemed
{
    private const int WM_PAINT = 0x000F;

    private readonly SuFontSync _fontSync;
    private string _placeholderText = string.Empty;
    private int _radius = -1;
    private bool _hovered;
    private bool _initializing = true;

    public SuComboBox()
    {
        _fontSync = new SuFontSync(this);

        DropDownStyle = ComboBoxStyle.DropDownList;
        DrawMode = DrawMode.OwnerDrawFixed;
        FlatStyle = FlatStyle.Flat;
        IntegralHeight = false;
        DropDownHeight = 240;
        MaxDropDownItems = 10;

        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);

        SuThemeManager.Register(this);
        ApplyTheme();
        _initializing = false;
    }

    /// <summary>未选中任何项时显示的占位文字。</summary>
    [Category("SuperUI")]
    [DefaultValue("")]
    public string PlaceholderText
    {
        get => _placeholderText;
        set
        {
            value ??= string.Empty;
            if (_placeholderText == value)
            {
                return;
            }

            _placeholderText = value;
            Invalidate();
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

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SuTheme Theme => SuThemeManager.Current;

    /// <inheritdoc />
    public virtual void OnThemeChanged()
    {
        ApplyTheme();
        Invalidate();
    }

    private void ApplyTheme()
    {
        _fontSync.Apply(Theme);
        BackColor = Theme.Surface;
        ForeColor = Theme.TextPrimary;

        var itemHeight = Math.Max(24, Font.Height + 12);
        if (ItemHeight != itemHeight)
        {
            ItemHeight = itemHeight;
        }

        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        PaintField(e.Graphics);
    }

    /// <inheritdoc />
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        // 原生 ComboBox 会自己画字段，这里在原生绘制之后整块覆盖成主题外观。
        if (m.Msg == WM_PAINT && IsHandleCreated)
        {
            using var graphics = CreateGraphics();
            PaintField(graphics);
        }
    }

    private void PaintField(Graphics graphics)
    {
        SuDraw.SetupQuality(graphics);

        var theme = Theme;
        var bounds = ClientRectangle;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        using (var background = new SolidBrush(BackColor))
        {
            graphics.FillRectangle(background, bounds);
        }

        var radius = _radius >= 0 ? _radius : theme.Radius;
        var rect = new RectangleF(0.5f, 0.5f, bounds.Width - 1f, bounds.Height - 1f);

        var surface = Enabled ? theme.Surface : theme.SurfaceAlt;
        var borderColor = !Enabled
            ? theme.Border
            : _hovered || Focused
                ? SuDraw.Blend(theme.Border, theme.Primary, Focused ? 0.9f : 0.45f)
                : theme.Border;

        using (var path = SuDraw.RoundedRect(rect, radius))
        {
            using (var brush = new SolidBrush(surface))
            {
                graphics.FillPath(brush, path);
            }

            using (var pen = new Pen(borderColor, Focused ? 1.5f : 1f))
            {
                graphics.DrawPath(pen, path);
            }
        }

        var arrowCenter = new PointF(bounds.Right - Padding.Right - 14f, bounds.Top + bounds.Height / 2f);
        using (var arrowPen = new Pen(Enabled ? theme.TextSecondary : theme.TextDisabled, 1.4f)
               {
                   StartCap = LineCap.Round,
                   EndCap = LineCap.Round,
               })
        {
            graphics.DrawLines(
                arrowPen,
                new[]
                {
                    new PointF(arrowCenter.X - 4f, arrowCenter.Y - 2f),
                    new PointF(arrowCenter.X, arrowCenter.Y + 2.5f),
                    new PointF(arrowCenter.X + 4f, arrowCenter.Y - 2f),
                });
        }

        var hasSelection = SelectedIndex >= 0;
        var text = hasSelection ? GetItemText(SelectedItem) : _placeholderText;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var textRect = new Rectangle(
            bounds.Left + Padding.Left + 12,
            bounds.Top,
            Math.Max(0, bounds.Width - Padding.Left - Padding.Right - 40),
            bounds.Height);

        TextRenderer.DrawText(
            graphics,
            text,
            Font,
            textRect,
            hasSelection
                ? (Enabled ? theme.TextPrimary : theme.TextDisabled)
                : theme.TextPlaceholder,
            TextFormatFlags.Left
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis
            | TextFormatFlags.NoPadding
            | TextFormatFlags.NoPrefix);
    }

    /// <inheritdoc />
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        var theme = Theme;
        var graphics = e.Graphics;

        if (e.Index < 0)
        {
            e.DrawBackground();
            return;
        }

        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var bounds = e.Bounds;

        using (var background = new SolidBrush(selected ? theme.PrimarySoft : theme.Surface))
        {
            graphics.FillRectangle(background, bounds);
        }

        var text = GetItemText(Items[e.Index]);
        var textRect = new Rectangle(bounds.Left + 10, bounds.Top, Math.Max(0, bounds.Width - 16), bounds.Height);

        TextRenderer.DrawText(
            graphics,
            text,
            Font,
            textRect,
            selected ? theme.Primary : theme.TextPrimary,
            TextFormatFlags.Left
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis
            | TextFormatFlags.NoPadding
            | TextFormatFlags.NoPrefix);
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        Invalidate();
        base.OnMouseLeave(e);
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
    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (_initializing || _fontSync is null)
        {
            return;
        }

        _fontSync.OnFontChanged();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SuThemeManager.Unregister(this);
        }

        base.Dispose(disposing);

        if (disposing)
        {
            _fontSync.Dispose();
        }
    }
}
