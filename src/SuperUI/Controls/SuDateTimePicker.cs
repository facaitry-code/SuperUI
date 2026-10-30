using System.ComponentModel;
using System.Globalization;
using SuperUI.Rendering;
using SuperUI.Theming;

namespace SuperUI.Controls;

/// <summary>
/// SuperUI 日期选择器：自绘字段（圆角、悬停/聚焦描边、日历按钮）+ 主题化日历弹层。
/// 点击字段或按 Enter / Space / Alt+↓ 打开日历，↑↓ 可以逐日调整。
/// </summary>
[Description("SuperUI 日期选择器：自绘字段 + 主题化日历弹层，颜色跟随全局主题。")]
[DefaultEvent(nameof(ValueChanged))]
[DefaultProperty(nameof(Value))]
public class SuDateTimePicker : SuControlBase
{
    private readonly SuTween _focusTween;
    private readonly MonthCalendar _calendar;
    private readonly ToolStripDropDown _dropDown;
    private DateTime _value = DateTime.Today;
    private DateTime _minimumDate = new(1900, 1, 1);
    private DateTime _maximumDate = new(9998, 12, 31);
    private string _displayFormat = "yyyy-MM-dd";
    private int _radius = -1;
    private bool _hovered;
    private bool _syncingCalendar;

    public SuDateTimePicker()
    {
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Hand;
        Padding = new Padding(12, 8, 10, 8);
        AutoSize = false;
        Size = new Size(180, 38);

        _focusTween = new SuTween();
        _focusTween.Changed += (_, __) => Invalidate();

        _calendar = new MonthCalendar
        {
            MaxSelectionCount = 1,
            ShowToday = true,
            ShowTodayCircle = true,
            ShowWeekNumbers = false,
            TabIndex = 0,
        };

        var host = new ToolStripControlHost(_calendar)
        {
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Size = _calendar.Size,
        };

        _dropDown = new ToolStripDropDown
        {
            AutoSize = true,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            DropShadowEnabled = true,
            AutoClose = true,
            Renderer = new SuDropDownRenderer(),
            BackColor = SuThemeManager.Current.Surface,
        };

        _dropDown.Items.Add(host);
        _dropDown.Closed += (_, __) => Invalidate();
        _calendar.DateSelected += CalendarOnDateSelected;

        SuThemeManager.Register(this);
        ApplyTheme();
    }

    /// <summary>日期变化。</summary>
    public event EventHandler? ValueChanged;

    /// <summary>当前日期。</summary>
    [Category("SuperUI")]
    [Description("当前日期。超出 MinDate/MaxDate 时会自动夹取。")]
    public DateTime Value
    {
        get => _value;
        set
        {
            var clamped = value.Date;
            if (clamped < _minimumDate)
            {
                clamped = _minimumDate;
            }

            if (clamped > _maximumDate)
            {
                clamped = _maximumDate;
            }

            if (_value == clamped)
            {
                UpdateCalendar();
                return;
            }

            _value = clamped;
            UpdateCalendar();
            ValueChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    /// <summary>最小可选日期。</summary>
    [Category("SuperUI")]
    [Description("最小可选日期。")]
    public DateTime MinDate
    {
        get => _minimumDate;
        set
        {
            if (_minimumDate == value.Date)
            {
                return;
            }

            _minimumDate = value.Date;
            ApplyCalendarTheme();
            Value = _value;
        }
    }

    /// <summary>最大可选日期。</summary>
    [Category("SuperUI")]
    [Description("最大可选日期。")]
    public DateTime MaxDate
    {
        get => _maximumDate;
        set
        {
            if (_maximumDate == value.Date)
            {
                return;
            }

            _maximumDate = value.Date;
            ApplyCalendarTheme();
            Value = _value;
        }
    }

    /// <summary>显示格式（默认 yyyy-MM-dd）。</summary>
    [Category("SuperUI")]
    [DefaultValue("yyyy-MM-dd")]
    public string DisplayFormat
    {
        get => _displayFormat;
        set
        {
            value = string.IsNullOrWhiteSpace(value) ? "yyyy-MM-dd" : value;
            if (_displayFormat == value)
            {
                return;
            }

            _displayFormat = value;
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

    /// <summary>日历弹层是否已打开。</summary>
    [Browsable(false)]
    public bool IsDropDownOpen => _dropDown.Visible;

    /// <summary>打开日历弹层。</summary>
    public void OpenCalendar()
    {
        if (_dropDown.Visible || !IsHandleCreated)
        {
            return;
        }

        UpdateCalendar();
        _dropDown.Show(this, new Point(0, Height + 2));
        _calendar.Focus();
        Invalidate();
    }

    /// <summary>关闭日历弹层。</summary>
    public void CloseCalendar()
    {
        if (_dropDown.Visible)
        {
            _dropDown.Close();
        }
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
        BackColor = SuBackdrop.Resolve(this);
        ApplyCalendarTheme();
        Invalidate();
    }

    private void ApplyCalendarTheme()
    {
        var theme = Theme;

        _calendar.BackColor = theme.Surface;
        _calendar.ForeColor = theme.TextPrimary;
        _calendar.TitleBackColor = theme.Primary;
        _calendar.TitleForeColor = theme.TextOnAccent;
        _calendar.TrailingForeColor = theme.TextPlaceholder;
        _calendar.MinDate = _minimumDate;
        _calendar.MaxDate = _maximumDate;
        _dropDown.BackColor = theme.Surface;
    }

    private void UpdateCalendar()
    {
        if (_syncingCalendar)
        {
            return;
        }

        _syncingCalendar = true;
        try
        {
            var date = _value;
            if (date < _calendar.MinDate)
            {
                date = _calendar.MinDate;
            }

            if (date > _calendar.MaxDate)
            {
                date = _calendar.MaxDate;
            }

            _calendar.SetDate(date);
        }
        finally
        {
            _syncingCalendar = false;
        }
    }

    private void CalendarOnDateSelected(object? sender, DateRangeEventArgs e)
    {
        if (_syncingCalendar)
        {
            return;
        }

        _syncingCalendar = true;
        try
        {
            Value = e.Start.Date;
        }
        finally
        {
            _syncingCalendar = false;
        }

        CloseCalendar();
        Focus();
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
        var focus = _focusTween.Value;
        var radius = _radius >= 0 ? _radius : theme.Radius;

        var surface = Enabled ? theme.Surface : theme.SurfaceAlt;
        var borderColor = theme.Border;
        if (!Enabled)
        {
            borderColor = theme.Border;
        }
        else if (_hovered)
        {
            borderColor = theme.BorderStrong;
        }

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

        DrawCalendarIcon(graphics, theme);

        var textRect = new Rectangle(
            Padding.Left,
            Padding.Top,
            Math.Max(0, Width - Padding.Horizontal - 26),
            Math.Max(0, Height - Padding.Vertical));

        TextRenderer.DrawText(
            graphics,
            _value.ToString(_displayFormat, CultureInfo.CurrentCulture),
            Font,
            textRect,
            Enabled ? theme.TextPrimary : theme.TextDisabled,
            TextFormatFlags.Left
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis
            | TextFormatFlags.NoPadding
            | TextFormatFlags.NoPrefix);
    }

    private void DrawCalendarIcon(Graphics graphics, SuTheme theme)
    {
        var color = Enabled ? theme.TextSecondary : theme.TextDisabled;
        var size = Math.Max(12, Font.Height - 2);
        var left = Width - Padding.Right - size - 2;
        var top = (Height - size) / 2f;

        var body = new RectangleF(left, top + size * 0.16f, size, size * 0.78f);
        using (var pen = new Pen(color, 1.2f))
        {
            using var path = SuDraw.RoundedRect(body, 2f);
            graphics.DrawPath(pen, path);

            var lineY = body.Top + size * 0.26f;
            graphics.DrawLine(pen, body.Left, lineY, body.Right, lineY);
            graphics.DrawLine(pen, left + size * 0.28f, top + size * 0.02f, left + size * 0.28f, top + size * 0.24f);
            graphics.DrawLine(pen, left + size * 0.72f, top + size * 0.02f, left + size * 0.72f, top + size * 0.24f);
        }

        using var dotBrush = new SolidBrush(color);
        var dotSize = Math.Max(1.6f, size * 0.13f);
        graphics.FillEllipse(dotBrush, body.Left + size * 0.2f, body.Top + size * 0.42f, dotSize, dotSize);
        graphics.FillEllipse(dotBrush, body.Left + size * 0.52f, body.Top + size * 0.42f, dotSize, dotSize);
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left || !Enabled)
        {
            return;
        }

        Focus();

        if (_dropDown.Visible)
        {
            CloseCalendar();
        }
        else
        {
            OpenCalendar();
        }
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
        _focusTween.To(1f, 120);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        _focusTween.To(0f, 160);
        Invalidate();
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData)
    {
        var code = keyData & Keys.KeyCode;
        if (code is Keys.Up or Keys.Down or Keys.Space or Keys.Enter)
        {
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
            case Keys.Enter:
            case Keys.Space:
                OpenCalendar();
                e.Handled = true;
                break;

            case Keys.Up when e.Alt:
                OpenCalendar();
                e.Handled = true;
                break;

            case Keys.Escape:
                CloseCalendar();
                e.Handled = true;
                break;

            case Keys.Up:
                Value = _value.AddDays(1);
                e.Handled = true;
                break;

            case Keys.Down:
                Value = _value.AddDays(-1);
                e.Handled = true;
                break;
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SuThemeManager.Unregister(this);
            _focusTween.Dispose();
            _dropDown.Close();
            _dropDown.Dispose();
            _calendar.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>去掉弹层默认边框/纯色背景，让日历自己撑满。</summary>
    private sealed class SuDropDownRenderer : ToolStripRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
        }
    }
}
