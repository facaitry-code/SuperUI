using System.ComponentModel;
using SuperUI.Rendering;
using SuperUI.Theming;

namespace SuperUI.Controls;

/// <summary>
/// SuperUI 进度条：圆角轨道 + 缓动填充条，支持百分比文字与“不确定”模式（来回滑动）。
/// </summary>
[Description("SuperUI 进度条：圆角轨道、缓动推进、可选百分比文字与不确定模式，颜色跟随全局主题。")]
[DefaultEvent(nameof(ValueChanged))]
[DefaultProperty(nameof(Value))]
public class SuProgressBar : SuControlBase
{
    private readonly SuTween _valueTween;
    private readonly System.Windows.Forms.Timer _marqueeTimer;
    private int _minimum;
    private int _maximum = 100;
    private int _value;
    private bool _isIndeterminate;
    private bool _showText;
    private Color? _barColor;
    private Color? _trackColor;
    private int _radius = -1;
    private float _marquee = -1f;
    private bool _marqueeForward = true;

    public SuProgressBar()
    {
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        Size = new Size(260, 10);

        _valueTween = new SuTween();
        _valueTween.Changed += (_, __) => Invalidate();

        _marqueeTimer = new System.Windows.Forms.Timer { Interval = 24 };
        _marqueeTimer.Tick += (_, __) =>
        {
            _marquee += _marqueeForward ? 0.03f : -0.03f;
            if (_marquee >= 1f)
            {
                _marquee = 1f;
                _marqueeForward = false;
            }
            else if (_marquee <= 0f)
            {
                _marquee = 0f;
                _marqueeForward = true;
            }

            Invalidate();
        };
    }

    /// <summary>进度值变化。</summary>
    public event EventHandler? ValueChanged;

    /// <summary>当前进度值。</summary>
    [Category("SuperUI")]
    [DefaultValue(0)]
    public int Value
    {
        get => _value;
        set
        {
            var clamped = Math.Min(_maximum, Math.Max(_minimum, value));
            if (_value == clamped)
            {
                return;
            }

            _value = clamped;
            _valueTween.To(Normalized, 200);
            ValueChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    /// <summary>最小值。</summary>
    [Category("SuperUI")]
    [DefaultValue(0)]
    public int Minimum
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

            Value = Math.Min(_maximum, Math.Max(_minimum, _value));
            _valueTween.Snap(Normalized);
            Invalidate();
        }
    }

    /// <summary>最大值。</summary>
    [Category("SuperUI")]
    [DefaultValue(100)]
    public int Maximum
    {
        get => _maximum;
        set
        {
            var maximum = value <= _minimum ? _minimum + 1 : value;
            if (_maximum == maximum)
            {
                return;
            }

            _maximum = maximum;
            Value = Math.Min(_maximum, Math.Max(_minimum, _value));
            _valueTween.Snap(Normalized);
            Invalidate();
        }
    }

    /// <summary>不确定模式：显示来回滑动的圆角条。</summary>
    [Category("SuperUI")]
    [DefaultValue(false)]
    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        set
        {
            if (_isIndeterminate == value)
            {
                return;
            }

            _isIndeterminate = value;
            if (value)
            {
                _marquee = 0f;
                _marqueeForward = true;
                _marqueeTimer.Start();
            }
            else
            {
                _marqueeTimer.Stop();
            }

            Invalidate();
        }
    }

    /// <summary>是否显示百分比文字。</summary>
    [Category("SuperUI")]
    [DefaultValue(false)]
    public bool ShowText
    {
        get => _showText;
        set
        {
            if (_showText == value)
            {
                return;
            }

            _showText = value;
            Invalidate();
        }
    }

    /// <summary>填充条颜色；null 表示主题主色。</summary>
    [Category("SuperUI")]
    [Description("填充条颜色，null 表示使用主题 Primary。")]
    public Color? BarColor
    {
        get => _barColor;
        set
        {
            if (_barColor == value)
            {
                return;
            }

            _barColor = value;
            Invalidate();
        }
    }

    /// <summary>轨道颜色；null 表示由主题推导。</summary>
    [Category("SuperUI")]
    [Description("轨道颜色，null 表示使用主题 SurfaceAlt。")]
    public Color? TrackColor
    {
        get => _trackColor;
        set
        {
            if (_trackColor == value)
            {
                return;
            }

            _trackColor = value;
            Invalidate();
        }
    }

    /// <summary>圆角半径；-1 表示按高度取胶囊形。</summary>
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

    /// <summary>进度归一化值（0..1）。</summary>
    private float Normalized
    {
        get
        {
            var range = _maximum - _minimum;
            return range <= 0 ? 0f : Math.Min(1f, Math.Max(0f, (_value - _minimum) / (float)range));
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
        var track = RectangleF.Inflate(ClientRectangle, -0.5f, -0.5f);
        if (track.Width <= 0 || track.Height <= 0)
        {
            return;
        }

        var radius = _radius >= 0 ? _radius : track.Height / 2f;
        var barColor = _barColor ?? theme.Primary;
        var trackColor = _trackColor ?? SuDraw.Blend(theme.SurfaceAlt, theme.Border, 0.35f);

        if (!Enabled)
        {
            barColor = SuDraw.Blend(barColor, theme.Background, 0.5f);
            trackColor = SuDraw.Blend(trackColor, theme.Background, 0.4f);
        }

        using (var trackPath = SuDraw.RoundedRect(track, radius))
        {
            using var trackBrush = new SolidBrush(trackColor);
            graphics.FillPath(trackBrush, trackPath);
        }

        if (_isIndeterminate)
        {
            DrawMarquee(graphics, track, radius, barColor);
        }
        else
        {
            DrawFill(graphics, track, radius, barColor);
        }

        if (_showText)
        {
            DrawText(graphics, theme, track, barColor);
        }
    }

    private void DrawFill(Graphics graphics, RectangleF track, float radius, Color barColor)
    {
        var progress = _valueTween.Value;
        if (progress <= 0.001f)
        {
            return;
        }

        var width = Math.Max(radius * 2f, track.Width * progress);
        width = Math.Min(width, track.Width);

        var fill = new RectangleF(track.Left, track.Top, width, track.Height);
        using var fillPath = SuDraw.RoundedRect(fill, radius);
        using var brush = new SolidBrush(barColor);
        graphics.FillPath(brush, fillPath);
    }

    private void DrawMarquee(Graphics graphics, RectangleF track, float radius, Color barColor)
    {
        var segmentWidth = Math.Max(radius * 2f, track.Width * 0.3f);
        var travel = Math.Max(1f, track.Width - segmentWidth);
        var left = track.Left + travel * _marquee;

        var segment = new RectangleF(left, track.Top, segmentWidth, track.Height);
        using var path = SuDraw.RoundedRect(segment, radius);
        using var brush = new SolidBrush(barColor);
        graphics.FillPath(brush, path);
    }

    private void DrawText(Graphics graphics, SuTheme theme, RectangleF track, Color barColor)
    {
        var text = _isIndeterminate
            ? string.Empty
            : $"{Math.Round(Normalized * 100)}%";

        if (text.Length == 0)
        {
            return;
        }

        var bounds = new Rectangle(
            (int)track.Left,
            (int)track.Top,
            (int)Math.Max(0, track.Width),
            (int)Math.Max(0, track.Height));

        // 文字中心落在填充条上时用反白色，否则用主文字色
        var centerX = bounds.Left + bounds.Width / 2f;
        var filled = !_isIndeterminate && centerX <= track.Left + track.Width * _valueTween.Value;
        var color = filled ? theme.TextOnAccent : theme.TextPrimary;

        TextRenderer.DrawText(
            graphics,
            text,
            Font,
            bounds,
            color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _valueTween.Dispose();
            _marqueeTimer.Stop();
            _marqueeTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}
