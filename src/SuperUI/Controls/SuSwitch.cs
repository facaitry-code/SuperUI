using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using SuperUI.Rendering;
using SuperUI.Theming;

namespace SuperUI.Controls;

/// <summary>
/// SuperUI 开关：胶囊轨道 + 滑动圆钮，切换时带缓动动画，可带文字标签（左/右）。
/// 颜色取自 <see cref="SuTheme"/>（轨道开=主色、关=中性灰、圆钮=反白色），随主题即时刷新。
/// </summary>
[Description("SuperUI 开关：胶囊轨道 + 滑动圆钮，带动画、可选文字标签，颜色跟随全局主题。")]
[DefaultEvent(nameof(CheckedChanged))]
[DefaultProperty(nameof(Checked))]
public class SuSwitch : SuControlBase
{
    private readonly SuTween _tween;
    private bool _checked;
    private bool _readOnly;
    private bool _labelOnLeft;
    private bool _hovered;
    private int _trackWidth = 46;
    private int _trackHeight = 24;
    private int _knobInset = 3;
    private int _labelGap = 10;
    private Color? _onColor;
    private Color? _offColor;
    private Color? _knobColor;

    public SuSwitch()
    {
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.CheckButton;
        SetStyle(ControlStyles.SupportsTransparentBackColor, false);

        _tween = new SuTween();
        _tween.Changed += (_, __) => Invalidate();

        AutoSize = false;
        Size = GetPreferredSize(Size.Empty);

        ApplyTheme();
    }

    /// <summary>开关状态变化。</summary>
    public event EventHandler? CheckedChanged;

    /// <summary>当前是否打开。</summary>
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
            _tween.To(value ? 1f : 0f, 150);
            OnCheckedChanged(EventArgs.Empty);
            Invalidate();
        }
    }

    /// <summary>只读：仍能聚焦，但点击 / 空格不会改变状态。</summary>
    [Category("SuperUI")]
    [DefaultValue(false)]
    public bool ReadOnly
    {
        get => _readOnly;
        set
        {
            if (_readOnly == value)
            {
                return;
            }

            _readOnly = value;
            Cursor = value ? Cursors.Default : Cursors.Hand;
            Invalidate();
        }
    }

    /// <summary>文字标签是否放在开关左侧（默认右侧）。</summary>
    [Category("SuperUI")]
    [DefaultValue(false)]
    public bool LabelOnLeft
    {
        get => _labelOnLeft;
        set
        {
            if (_labelOnLeft == value)
            {
                return;
            }

            _labelOnLeft = value;
            AdjustSize();
            Invalidate();
        }
    }

    /// <summary>轨道宽度。</summary>
    [Category("SuperUI")]
    [DefaultValue(46)]
    public int TrackWidth
    {
        get => _trackWidth;
        set
        {
            var width = Math.Max(20, value);
            if (_trackWidth == width)
            {
                return;
            }

            _trackWidth = width;
            AdjustSize();
            Invalidate();
        }
    }

    /// <summary>轨道高度（圆钮大小由它推导）。</summary>
    [Category("SuperUI")]
    [DefaultValue(24)]
    public int TrackHeight
    {
        get => _trackHeight;
        set
        {
            var height = Math.Max(12, value);
            if (_trackHeight == height)
            {
                return;
            }

            _trackHeight = height;
            AdjustSize();
            Invalidate();
        }
    }

    /// <summary>圆钮相对轨道的内边距。</summary>
    [Category("SuperUI")]
    [DefaultValue(3)]
    public int KnobInset
    {
        get => _knobInset;
        set
        {
            var inset = Math.Max(1, value);
            if (_knobInset == inset)
            {
                return;
            }

            _knobInset = inset;
            Invalidate();
        }
    }

    /// <summary>标签与开关之间的间距。</summary>
    [Category("SuperUI")]
    [DefaultValue(10)]
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

    /// <summary>打开时的轨道颜色；null 表示用主题主色。</summary>
    [Category("SuperUI")]
    [Description("打开状态的轨道颜色，null 表示使用主题 Primary。")]
    public Color? OnColor
    {
        get => _onColor;
        set
        {
            if (_onColor == value)
            {
                return;
            }

            _onColor = value;
            Invalidate();
        }
    }

    /// <summary>关闭时的轨道颜色；null 表示由主题推导的中性灰。</summary>
    [Category("SuperUI")]
    [Description("关闭状态的轨道颜色，null 表示由主题推导的中性灰。")]
    public Color? OffColor
    {
        get => _offColor;
        set
        {
            if (_offColor == value)
            {
                return;
            }

            _offColor = value;
            Invalidate();
        }
    }

    /// <summary>圆钮颜色；null 表示反白色。</summary>
    [Category("SuperUI")]
    [Description("圆钮颜色，null 表示使用主题 TextOnAccent（反白）。")]
    public Color? KnobColor
    {
        get => _knobColor;
        set
        {
            if (_knobColor == value)
            {
                return;
            }

            _knobColor = value;
            Invalidate();
        }
    }

    /// <summary>文字标签（同时用于可访问性名称）。</summary>
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
        var height = Math.Max(_trackHeight + 4, Font.Height + 4);
        var width = _trackWidth + 4;

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
        AccessibleName = Text;
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

    /// <summary>轨道矩形（已按标签位置与控件高度居中）。</summary>
    private RectangleF GetTrackRect()
    {
        var width = Math.Min(_trackWidth, Math.Max(12, Width - 4));
        var height = Math.Min(_trackHeight, Math.Max(10, Height - 4));
        var left = _labelOnLeft ? Width - width - 2f : 2f;
        var top = (Height - height) / 2f;
        return new RectangleF(left, top, width, height);
    }

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
        var track = GetTrackRect();
        var radius = track.Height / 2f;
        var progress = _tween.Value;

        var onColor = _onColor ?? theme.Primary;
        var offColor = _offColor ?? SuDraw.Blend(theme.SurfaceAlt, theme.Border, 0.5f);
        var knobColor = _knobColor ?? theme.TextOnAccent;

        if (!Enabled)
        {
            onColor = SuDraw.Blend(onColor, theme.Background, 0.55f);
            offColor = SuDraw.Blend(offColor, theme.Background, 0.45f);
            knobColor = SuDraw.Blend(knobColor, theme.Background, 0.3f);
        }

        var trackColor = SuDraw.Blend(offColor, onColor, progress);

        if (_hovered && Enabled && !_readOnly)
        {
            trackColor = SuDraw.Blend(trackColor, theme.IsDark ? Color.White : Color.Black, 0.06f);
        }

        using (var trackPath = SuDraw.RoundedRect(track, radius))
        {
            using (var brush = new SolidBrush(trackColor))
            {
                graphics.FillPath(brush, trackPath);
            }

            if (!Enabled)
            {
                using var pen = new Pen(SuDraw.Alpha(theme.Border, 140), 1f);
                graphics.DrawPath(pen, trackPath);
            }
        }

        var inset = (float)_knobInset;
        var knobSize = Math.Max(6f, track.Height - inset * 2f);
        var knobLeft = track.Left + inset + (track.Width - knobSize - inset * 2f) * progress;
        var knobRect = new RectangleF(knobLeft, track.Top + inset, knobSize, knobSize);

        // 轻微投影，让白色圆钮在浅色轨道上也有层次
        var shadowRect = new RectangleF(knobRect.X, knobRect.Y + 1f, knobRect.Width, knobRect.Height);
        using (var shadow = new SolidBrush(SuDraw.Alpha(Color.Black, Enabled ? 45 : 20)))
        {
            graphics.FillEllipse(shadow, shadowRect);
        }

        using (var knob = new SolidBrush(knobColor))
        {
            graphics.FillEllipse(knob, knobRect);
        }

        if (Focused && ShowFocusCues && Enabled)
        {
            var ring = RectangleF.Inflate(track, 2f, 2f);
            using var ringPath = SuDraw.RoundedRect(ring, radius + 2f);
            using var ringPen = new Pen(SuDraw.Alpha(theme.Primary, 120), 1.4f);
            graphics.DrawPath(ringPen, ringPath);
        }

        if (!string.IsNullOrEmpty(Text))
        {
            var labelRect = _labelOnLeft
                ? new Rectangle(2, 0, Math.Max(0, (int)track.Left - _labelGap - 2), Height)
                : new Rectangle((int)track.Right + _labelGap, 0, Math.Max(0, Width - (int)track.Right - _labelGap - 2), Height);

            if (labelRect.Width > 0)
            {
                TextRenderer.DrawText(
                    graphics,
                    Text,
                    Font,
                    labelRect,
                    Enabled ? theme.TextPrimary : theme.TextDisabled,
                    TextFormatFlags.Left
                    | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.EndEllipsis
                    | TextFormatFlags.NoPadding
                    | TextFormatFlags.NoPrefix);
            }
        }
    }

    /// <inheritdoc />
    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);

        if (!Enabled || _readOnly)
        {
            return;
        }

        Checked = !Checked;
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData)
        => (keyData & Keys.KeyCode) == Keys.Space || base.IsInputKey(keyData);

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space)
        {
            if (Enabled && !_readOnly)
            {
                Checked = !Checked;
            }

            e.Handled = true;
        }

        base.OnKeyDown(e);
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
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        AdjustSize();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        AccessibleName = Text;
        AdjustSize();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Cursor = Enabled && !_readOnly ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    /// <summary>触发 <see cref="CheckedChanged"/>。</summary>
    protected virtual void OnCheckedChanged(EventArgs e) => CheckedChanged?.Invoke(this, e);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tween.Dispose();
        }

        base.Dispose(disposing);
    }
}
