using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using SuperUI.Rendering;
using SuperUI.Theming;

namespace SuperUI.Controls;

/// <summary>
/// 选项类控件（复选框 / 单选按钮）的公共基类：左侧图形 + 右侧文字、悬停缓动、
/// 聚焦光环、空格键切换、按文字自动尺寸。
/// </summary>
[ToolboxItem(false)]
public abstract class SuOptionBase : SuControlBase
{
    private readonly SuTween _hoverTween;
    private bool _checked;
    private bool _hovered;
    private int _boxSize = 18;
    private int _labelGap = 8;

    protected SuOptionBase()
    {
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Hand;
        Padding = new Padding(2, 2, 2, 2);
        AutoSize = false;

        _hoverTween = new SuTween();
        _hoverTween.Changed += (_, __) => Invalidate();
    }

    /// <summary>选中状态变化。</summary>
    public event EventHandler? CheckedChanged;

    /// <summary>是否选中。</summary>
    [Category("SuperUI")]
    [DefaultValue(false)]
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
            {
                return;
            }

            _checked = value;
            if (value)
            {
                OnCheckedOn();
            }
            else
            {
                OnCheckedOff();
            }

            OnCheckedChanged(EventArgs.Empty);
            Invalidate();
        }
    }

    /// <summary>图形方块大小。</summary>
    [Category("SuperUI")]
    [DefaultValue(18)]
    public int BoxSize
    {
        get => _boxSize;
        set
        {
            var size = Math.Max(12, value);
            if (_boxSize == size)
            {
                return;
            }

            _boxSize = size;
            AdjustSize();
            Invalidate();
        }
    }

    /// <summary>图形与文字的间距。</summary>
    [Category("SuperUI")]
    [DefaultValue(8)]
    public int LabelGap
    {
        get => _labelGap;
        set
        {
            var gap = Math.Max(0, value);
            if (_labelGap == gap)
            {
                return;
            }

            _labelGap = gap;
            AdjustSize();
            Invalidate();
        }
    }

    /// <summary>文字标签。</summary>
    [Category("SuperUI")]
    [DefaultValue("")]
    [AllowNull]
    public override string Text
    {
        get => base.Text;
        set
        {
            base.Text = value ?? string.Empty;
            AdjustSize();
            Invalidate();
        }
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize)
    {
        var height = Math.Max(_boxSize + 4, Font.Height + 4);
        var width = _boxSize + 4;

        if (!string.IsNullOrEmpty(Text))
        {
            var textSize = TextRenderer.MeasureText(
                Text,
                Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

            width += _labelGap + textSize.Width;
        }

        return new Size(width, height);
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
        AdjustSize();
    }

    private void AdjustSize()
    {
        var size = GetPreferredSize(Size.Empty);
        if (Size != size)
        {
            Size = size;
        }
    }

    /// <summary>图形区域（垂直居中）。</summary>
    protected RectangleF GetGlyphRect()
    {
        var top = (Height - _boxSize) / 2f;
        return new RectangleF(Padding.Left, top, _boxSize, _boxSize);
    }

    /// <summary>文字区域。</summary>
    protected Rectangle GetTextRect(RectangleF glyph)
    {
        var left = (int)glyph.Right + _labelGap;
        return new Rectangle(left, 0, Math.Max(0, Width - left - Padding.Right), Height);
    }

    /// <summary>子类在这里绘制自己的图形（勾选框 / 单选圈）。</summary>
    protected abstract void DrawGlyph(
        Graphics graphics,
        SuTheme theme,
        RectangleF glyph,
        bool isChecked,
        bool enabled,
        bool hovered);

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        SuDraw.SetupQuality(graphics);

        using (var clear = new SolidBrush(BackColor))
        {
            graphics.FillRectangle(clear, ClientRectangle);
        }

        var theme = Theme;
        var glyph = GetGlyphRect();
        var hover = _hoverTween.Value;

        DrawGlyph(graphics, theme, glyph, _checked, Enabled, _hovered || hover > 0.01f);

        if (Focused && ShowFocusCues && Enabled)
        {
            var ring = RectangleF.Inflate(glyph, 2f, 2f);
            using var ringPath = SuDraw.RoundedRect(ring, Math.Max(2f, theme.Radius - 1f));
            using var ringPen = new Pen(SuDraw.Alpha(theme.Primary, 110), 1.2f);
            graphics.DrawPath(ringPen, ringPath);
        }

        if (!string.IsNullOrEmpty(Text))
        {
            var textRect = GetTextRect(glyph);
            if (textRect.Width > 0)
            {
                TextRenderer.DrawText(
                    graphics,
                    Text,
                    Font,
                    textRect,
                    Enabled ? theme.TextPrimary : theme.TextDisabled,
                    TextFormatFlags.Left
                    | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.EndEllipsis
                    | TextFormatFlags.NoPadding
                    | TextFormatFlags.NoPrefix);
            }
        }
    }

    /// <summary>勾选时的行为（单选按钮会取消同组其它项）。</summary>
    protected virtual void OnCheckedOn()
    {
    }

    /// <summary>取消勾选时的行为。</summary>
    protected virtual void OnCheckedOff()
    {
    }

    /// <summary>触发 <see cref="CheckedChanged"/>。</summary>
    protected virtual void OnCheckedChanged(EventArgs e) => CheckedChanged?.Invoke(this, e);

    /// <inheritdoc />
    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);

        if (Enabled)
        {
            Checked = !Checked;
        }
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData)
        => (keyData & Keys.KeyCode) == Keys.Space || base.IsInputKey(keyData);

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space && Enabled)
        {
            Checked = !Checked;
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        _hoverTween.To(1f, 120);
        base.OnMouseEnter(e);
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _hoverTween.To(0f, 140);
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
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        AdjustSize();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
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
