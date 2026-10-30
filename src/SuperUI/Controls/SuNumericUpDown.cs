using System.ComponentModel;
using System.Globalization;
using SuperUI.Rendering;
using SuperUI.Theming;

namespace SuperUI.Controls;

/// <summary>
/// SuperUI 数值输入框：圆角外框 + 右侧上下步进按钮 + 可选单位后缀，
/// 支持键入 / 滚轮 / ↑↓ / 长按步进，越界自动夹取，颜色与字体跟随全局主题。
/// </summary>
[Description("SuperUI 数值输入框：圆角外框、自绘步进按钮、单位后缀、滚轮与键盘步进，颜色跟随全局主题。")]
[DefaultEvent(nameof(ValueChanged))]
[DefaultProperty(nameof(Value))]
public class SuNumericUpDown : SuControlBase
{
    private readonly SuTween _focusTween;
    private readonly TextBox _inner;
    private readonly System.Windows.Forms.Timer _repeatTimer;
    private decimal _value;
    private decimal _minimum;
    private decimal _maximum = 100m;
    private decimal _increment = 1m;
    private int _decimalPlaces;
    private bool _thousandsSeparator;
    private string _unit = string.Empty;
    private int _radius = -1;
    private int _buttonWidth = 24;
    private bool _showSpinButtons = true;
    private bool _autoHeight = true;
    private bool _hovered;
    private bool _updating;
    private int _spinHover;
    private int _spinPressed;
    private int _unitWidth;

    public SuNumericUpDown()
    {
        SetStyle(ControlStyles.Selectable, true);
        Padding = new Padding(12, 8, 8, 8);
        Cursor = Cursors.IBeam;
        AccessibleRole = AccessibleRole.SpinButton;

        _focusTween = new SuTween();
        _focusTween.Changed += (_, __) => Invalidate();

        _inner = new TextBox
        {
            BorderStyle = BorderStyle.None,
            TabStop = false,
            TextAlign = HorizontalAlignment.Left,
        };

        Controls.Add(_inner);

        _inner.TextChanged += InnerOnTextChanged;
        _inner.GotFocus += (_, __) =>
        {
            _focusTween.To(1f, 120);
            _inner.SelectAll();
            Invalidate();
        };
        _inner.LostFocus += (_, __) =>
        {
            _focusTween.To(0f, 160);
            CommitText();
            Invalidate();
        };
        _inner.KeyDown += InnerOnKeyDown;
        _inner.KeyPress += InnerOnKeyPress;
        _inner.MouseEnter += (_, __) => SetHovered(true);
        _inner.MouseLeave += (_, __) => RefreshHoverFromCursor();

        _repeatTimer = new System.Windows.Forms.Timer { Interval = 380 };
        _repeatTimer.Tick += RepeatTimerOnTick;

        ApplyTheme();
        UpdateText();
        Height = PreferredHeight;
    }

    /// <summary>数值变化（越界夹取后与旧值不同才触发）。</summary>
    public event EventHandler? ValueChanged;

    /// <summary>当前数值。</summary>
    [Category("SuperUI")]
    [DefaultValue(0)]
    public decimal Value
    {
        get => _value;
        set => SetValue(value, true);
    }

    /// <summary>允许的最小值。</summary>
    [Category("SuperUI")]
    [DefaultValue(0)]
    public decimal Minimum
    {
        get => _minimum;
        set
        {
            if (_minimum == value)
            {
                return;
            }

            _minimum = value;
            if (_maximum < _minimum)
            {
                _maximum = _minimum;
            }

            SetValue(_value, false);
            Invalidate();
        }
    }

    /// <summary>允许的最大值。</summary>
    [Category("SuperUI")]
    [DefaultValue(100)]
    public decimal Maximum
    {
        get => _maximum;
        set
        {
            if (_maximum == value)
            {
                return;
            }

            _maximum = value;
            if (_minimum > _maximum)
            {
                _minimum = _maximum;
            }

            SetValue(_value, false);
            Invalidate();
        }
    }

    /// <summary>每次步进的增量。</summary>
    [Category("SuperUI")]
    [DefaultValue(1)]
    public decimal Increment
    {
        get => _increment;
        set
        {
            var increment = value == 0m ? 1m : Math.Abs(value);
            if (_increment == increment)
            {
                return;
            }

            _increment = increment;
        }
    }

    /// <summary>小数位数（0 表示只允许整数）。</summary>
    [Category("SuperUI")]
    [DefaultValue(0)]
    public int DecimalPlaces
    {
        get => _decimalPlaces;
        set
        {
            var places = Math.Max(0, Math.Min(8, value));
            if (_decimalPlaces == places)
            {
                return;
            }

            _decimalPlaces = places;
            SetValue(_value, false);
            Invalidate();
        }
    }

    /// <summary>是否显示千位分隔符。</summary>
    [Category("SuperUI")]
    [DefaultValue(false)]
    public bool ThousandsSeparator
    {
        get => _thousandsSeparator;
        set
        {
            if (_thousandsSeparator == value)
            {
                return;
            }

            _thousandsSeparator = value;
            UpdateText();
            Invalidate();
        }
    }

    /// <summary>单位后缀（如 %、px、kg），为空则不显示。</summary>
    [Category("SuperUI")]
    [DefaultValue("")]
    public string Unit
    {
        get => _unit;
        set
        {
            value ??= string.Empty;
            if (_unit == value)
            {
                return;
            }

            _unit = value;
            LayoutInner();
            Invalidate();
        }
    }

    /// <summary>只读：可选中复制，但不能键入或步进。</summary>
    [Category("SuperUI")]
    [DefaultValue(false)]
    public bool ReadOnly
    {
        get => _inner.ReadOnly;
        set
        {
            if (_inner.ReadOnly == value)
            {
                return;
            }

            _inner.ReadOnly = value;
            Cursor = value ? Cursors.Default : Cursors.IBeam;
            ApplyColors();
            Invalidate();
        }
    }

    /// <summary>是否显示右侧上下步进按钮。</summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    public bool ShowSpinButtons
    {
        get => _showSpinButtons;
        set
        {
            if (_showSpinButtons == value)
            {
                return;
            }

            _showSpinButtons = value;
            LayoutInner();
            Invalidate();
        }
    }

    /// <summary>步进按钮宽度。</summary>
    [Category("SuperUI")]
    [DefaultValue(24)]
    public int ButtonWidth
    {
        get => _buttonWidth;
        set
        {
            var width = Math.Max(14, value);
            if (_buttonWidth == width)
            {
                return;
            }

            _buttonWidth = width;
            LayoutInner();
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

    /// <summary>是否根据字体与内边距自动计算高度。</summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    public bool AutoHeight
    {
        get => _autoHeight;
        set
        {
            if (_autoHeight == value)
            {
                return;
            }

            _autoHeight = value;
            if (value)
            {
                Height = PreferredHeight;
            }
        }
    }

    /// <summary>建议高度。</summary>
    [Browsable(false)]
    public int PreferredHeight => _inner is null ? 38 : _inner.PreferredHeight + Padding.Vertical + 2;

    /// <summary>内部原生输入框（高级用法）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextBox InnerTextBox => _inner;

    /// <summary>加一次增量。</summary>
    public void Up() => Step(1);

    /// <summary>减一次增量。</summary>
    public void Down() => Step(-1);

    /// <summary>选中全部文本。</summary>
    public void SelectAll() => _inner.SelectAll();

    /// <summary>让输入区获得焦点。</summary>
    public new bool Focus() => _inner.Focus();

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
        ApplyColors();
        LayoutInner();
        if (_autoHeight)
        {
            Height = PreferredHeight;
        }
    }

    private void ApplyColors()
    {
        if (_inner is null)
        {
            return;
        }

        var theme = Theme;
        var surface = ReadOnly || !Enabled ? theme.SurfaceAlt : theme.Surface;
        _inner.BackColor = surface;
        _inner.ForeColor = Enabled ? theme.TextPrimary : theme.TextDisabled;
    }

    // ---------------- 数值 ----------------

    private void SetValue(decimal value, bool raiseEvent)
    {
        var clamped = Math.Round(Math.Min(_maximum, Math.Max(_minimum, value)), _decimalPlaces);
        var changed = clamped != _value;
        _value = clamped;

        UpdateText();
        if (changed && raiseEvent)
        {
            OnValueChanged(EventArgs.Empty);
        }

        Invalidate();
    }

    private void Step(int direction)
    {
        if (!Enabled || ReadOnly)
        {
            return;
        }

        CommitText();
        SetValue(_value + _increment * direction, true);
    }

    /// <summary>把输入框文本解析回数值（越界夹取并回写格式化文本）。</summary>
    private void CommitText()
    {
        if (_updating || _inner is null)
        {
            return;
        }

        if (!TryParse(_inner.Text, out var parsed))
        {
            UpdateText();
            return;
        }

        SetValue(parsed, true);
    }

    private bool TryParse(string? text, out decimal result)
    {
        text = (text ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            result = 0m;
            return false;
        }

        const NumberStyles styles = NumberStyles.Number;
        return decimal.TryParse(text, styles, CultureInfo.CurrentCulture, out result)
               || decimal.TryParse(text, styles, CultureInfo.InvariantCulture, out result);
    }

    private string FormatValue()
    {
        var format = _thousandsSeparator
            ? (_decimalPlaces > 0 ? "N" + _decimalPlaces : "N0")
            : (_decimalPlaces > 0 ? "F" + _decimalPlaces : "F0");

        return _value.ToString(format, CultureInfo.CurrentCulture);
    }

    private void UpdateText()
    {
        if (_inner is null)
        {
            return;
        }

        var text = FormatValue();
        if (_inner.Text == text)
        {
            return;
        }

        _updating = true;
        try
        {
            _inner.Text = text;
        }
        finally
        {
            _updating = false;
        }

        AccessibleName = string.IsNullOrEmpty(_unit) ? text : text + _unit;
    }

    private void OnValueChanged(EventArgs e)
    {
        if (!string.IsNullOrEmpty(_unit))
        {
            AccessibleName = FormatValue() + _unit;
        }

        ValueChanged?.Invoke(this, e);
    }

    // ---------------- 布局 ----------------

    private Rectangle ContentArea
        => new(
            Padding.Left,
            Padding.Top,
            Math.Max(0, ClientSize.Width - Padding.Horizontal),
            Math.Max(0, ClientSize.Height - Padding.Vertical));

    private Rectangle SpinArea
    {
        get
        {
            if (!_showSpinButtons)
            {
                return Rectangle.Empty;
            }

            var area = ContentArea;
            return new Rectangle(
                Math.Max(area.Left, area.Right - _buttonWidth),
                area.Top,
                _buttonWidth,
                area.Height);
        }
    }

    private Rectangle UpArea
    {
        get
        {
            var spin = SpinArea;
            return spin.IsEmpty ? Rectangle.Empty : new Rectangle(spin.Left, spin.Top, spin.Width, spin.Height / 2);
        }
    }

    private Rectangle DownArea
    {
        get
        {
            var spin = SpinArea;
            return spin.IsEmpty
                ? Rectangle.Empty
                : new Rectangle(spin.Left, spin.Top + spin.Height / 2, spin.Width, spin.Height - spin.Height / 2);
        }
    }

    private void LayoutInner()
    {
        if (_inner is null)
        {
            return;
        }

        _unitWidth = string.IsNullOrEmpty(_unit)
            ? 0
            : TextRenderer.MeasureText(
                _unit,
                Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width + 6;

        var area = ContentArea;
        var rightReserve = (_showSpinButtons ? _buttonWidth : 0) + _unitWidth;
        var innerWidth = Math.Max(12, area.Width - rightReserve);
        var innerHeight = _inner.PreferredHeight;
        var top = area.Top + Math.Max(0, (area.Height - innerHeight) / 2);

        _inner.Bounds = new Rectangle(area.Left, top, innerWidth, innerHeight);
    }

    // ---------------- 绘制 ----------------

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
        var focus = _focusTween.Value;
        var radius = _radius >= 0 ? _radius : theme.Radius;

        var surface = ReadOnly || !Enabled ? theme.SurfaceAlt : theme.Surface;
        if (!Enabled)
        {
            surface = SuDraw.Blend(surface, theme.Background, 0.35f);
        }

        var borderColor = _hovered && !_inner.Focused ? theme.BorderStrong : theme.Border;
        borderColor = SuDraw.Blend(borderColor, theme.Primary, focus * 0.9f);

        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        using (var path = SuDraw.RoundedRect(rect, radius))
        {
            using (var brush = new SolidBrush(surface))
            {
                graphics.FillPath(brush, path);
            }

            using (var pen = new Pen(borderColor, 1f + focus * 0.6f))
            {
                graphics.DrawPath(pen, path);
            }
        }

        if (focus > 0.01f)
        {
            var inner = RectangleF.Inflate(rect, -2.5f, -2.5f);
            using var innerPath = SuDraw.RoundedRect(inner, Math.Max(0f, radius - 2f));
            using var ringPen = new Pen(SuDraw.Alpha(theme.FocusRing, (int)(60 * focus)), 1f);
            graphics.DrawPath(ringPen, innerPath);
        }

        DrawUnit(graphics, theme);
        DrawSpinButtons(graphics, theme, radius);
    }

    private void DrawUnit(Graphics graphics, SuTheme theme)
    {
        if (_unitWidth <= 0)
        {
            return;
        }

        var area = ContentArea;
        var spin = SpinArea;
        var right = spin.IsEmpty ? area.Right : spin.Left;
        var unitRect = new Rectangle(right - _unitWidth, area.Top, _unitWidth - 2, area.Height);

        if (unitRect.Width <= 0)
        {
            return;
        }

        TextRenderer.DrawText(
            graphics,
            _unit,
            Font,
            unitRect,
            Enabled ? theme.TextSecondary : theme.TextDisabled,
            TextFormatFlags.Right
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.NoPadding
            | TextFormatFlags.NoPrefix);
    }

    private void DrawSpinButtons(Graphics graphics, SuTheme theme, int radius)
    {
        if (!_showSpinButtons)
        {
            return;
        }

        DrawSpinButton(graphics, theme, UpArea, 1, radius);
        DrawSpinButton(graphics, theme, DownArea, -1, radius);
    }

    private void DrawSpinButton(Graphics graphics, SuTheme theme, Rectangle area, int direction, int radius)
    {
        if (area.IsEmpty || area.Width <= 0 || area.Height <= 0)
        {
            return;
        }

        var interactive = Enabled && !ReadOnly;
        var hovered = interactive && _spinHover == direction;
        var pressed = interactive && _spinPressed == direction;

        if (hovered || pressed)
        {
            var buttonRect = Rectangle.Inflate(DefaultRect(area), 0, -1);
            var fill = pressed
                ? SuDraw.Blend(theme.SurfaceAlt, theme.Border, 0.55f)
                : theme.SurfaceAlt;

            using var path = SuDraw.RoundedRect(buttonRect, Math.Max(3, radius - 3));
            using var brush = new SolidBrush(fill);
            graphics.FillPath(brush, path);
        }

        var color = !interactive
            ? theme.TextDisabled
            : hovered || pressed
                ? theme.TextPrimary
                : theme.TextSecondary;

        DrawTriangle(graphics, area, direction > 0, color);
    }

    private static Rectangle DefaultRect(Rectangle area) => new(area.Left + 2, area.Top + 1, Math.Max(0, area.Width - 6), Math.Max(0, area.Height - 2));

    private static void DrawTriangle(Graphics graphics, Rectangle area, bool up, Color color)
    {
        var cx = area.Left + area.Width / 2f;
        var cy = area.Top + area.Height / 2f;
        const float halfWidth = 4.5f;
        const float halfHeight = 2.6f;

        var points = up
            ? new[]
            {
                new PointF(cx, cy - halfHeight),
                new PointF(cx + halfWidth, cy + halfHeight),
                new PointF(cx - halfWidth, cy + halfHeight),
            }
            : new[]
            {
                new PointF(cx, cy + halfHeight),
                new PointF(cx - halfWidth, cy - halfHeight),
                new PointF(cx + halfWidth, cy - halfHeight),
            };

        using var brush = new SolidBrush(color);
        graphics.FillPolygon(brush, points);
    }

    // ---------------- 交互 ----------------

    private void InnerOnTextChanged(object? sender, EventArgs e)
    {
        if (!_updating)
        {
            OnTextChanged(e);
        }
    }

    private void InnerOnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Up:
                Up();
                e.Handled = true;
                e.SuppressKeyPress = true;
                break;

            case Keys.Down:
                Down();
                e.Handled = true;
                e.SuppressKeyPress = true;
                break;

            case Keys.Enter:
                CommitText();
                _inner.SelectAll();
                e.Handled = true;
                e.SuppressKeyPress = true;
                break;

            case Keys.Escape:
                UpdateText();
                e.Handled = true;
                break;
        }
    }

    private void InnerOnKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (char.IsControl(e.KeyChar) || char.IsDigit(e.KeyChar))
        {
            return;
        }

        var format = CultureInfo.CurrentCulture.NumberFormat;

        if (_decimalPlaces > 0 && format.NumberDecimalSeparator.Length == 1
            && e.KeyChar.ToString() == format.NumberDecimalSeparator)
        {
            return;
        }

        if (_thousandsSeparator && format.NumberGroupSeparator.Length == 1
            && e.KeyChar.ToString() == format.NumberGroupSeparator)
        {
            return;
        }

        if (e.KeyChar == '-' && _minimum < 0m && _inner.SelectionStart == 0)
        {
            return;
        }

        if (!char.IsControl(e.KeyChar))
        {
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        CommitText();

        if (Enabled && !ReadOnly && UpArea.Contains(e.Location))
        {
            _spinPressed = 1;
            Step(1);
            StartRepeat();
            Invalidate();
        }
        else if (Enabled && !ReadOnly && DownArea.Contains(e.Location))
        {
            _spinPressed = -1;
            Step(-1);
            StartRepeat();
            Invalidate();
        }
        else if (!_inner.Focused)
        {
            _inner.Focus();
        }
    }

    /// <inheritdoc />
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button == MouseButtons.Left && _spinPressed != 0)
        {
            _spinPressed = 0;
            StopRepeat();
            Invalidate();
        }
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var hover = UpArea.Contains(e.Location) ? 1 : DownArea.Contains(e.Location) ? -1 : 0;
        if (hover != _spinHover)
        {
            _spinHover = hover;
            Cursor = hover != 0 && Enabled && !ReadOnly ? Cursors.Hand : ReadOnly ? Cursors.Default : Cursors.IBeam;
            Invalidate();
        }
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(EventArgs e)
    {
        SetHovered(true);
        base.OnMouseEnter(e);
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        RefreshHoverFromCursor();

        if (_spinHover != 0)
        {
            _spinHover = 0;
            Invalidate();
        }

        base.OnMouseLeave(e);
    }

    /// <inheritdoc />
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        if (!Enabled || ReadOnly)
        {
            return;
        }

        if (e.Delta > 0)
        {
            Up();
        }
        else if (e.Delta < 0)
        {
            Down();
        }
    }

    private void StartRepeat()
    {
        _repeatTimer.Interval = 380;
        _repeatTimer.Start();
    }

    private void StopRepeat() => _repeatTimer.Stop();

    private void RepeatTimerOnTick(object? sender, EventArgs e)
    {
        if (_spinPressed == 0)
        {
            _repeatTimer.Stop();
            return;
        }

        _repeatTimer.Interval = 70;
        Step(_spinPressed);
    }

    private void SetHovered(bool value)
    {
        if (_hovered == value)
        {
            return;
        }

        _hovered = value;
        Invalidate();
    }

    private void RefreshHoverFromCursor()
    {
        var inside = ClientRectangle.Contains(PointToClient(Cursor.Position));
        SetHovered(inside);
    }

    /// <inheritdoc />
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutInner();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        LayoutInner();
        if (_autoHeight && !_updating)
        {
            Height = PreferredHeight;
        }

        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        ApplyColors();
        Invalidate(true);
    }

    /// <inheritdoc />
    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        _inner.Focus();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _focusTween.Dispose();
            _repeatTimer.Stop();
            _repeatTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}
