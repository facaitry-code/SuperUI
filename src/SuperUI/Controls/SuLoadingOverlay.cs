using System.ComponentModel;
using System.Runtime.CompilerServices;
using SuperUI.Rendering;
using SuperUI.Theming;

namespace SuperUI.Controls;

/// <summary>
/// SuperUI 遮盖层（Loading 遮罩）：铺满目标控件，先铺一层目标区域的快照、再叠半透明遮罩，
/// 中间显示旋转指示器与提示文字；显示期间屏蔽目标区域内的键盘与滚轮输入。
/// </summary>
/// <example>
/// <code>
/// SuLoadingOverlay.Show(this, "正在加载数据…");   // 静态快捷方式
/// SuLoadingOverlay.Hide(this);
///
/// var overlay = SuLoadingOverlay.For(myPanel);     // 或挂在某个面板上
/// overlay.ShowLoading("保存中…");
/// overlay.HideLoading();
/// </code>
/// </example>
[Description("SuperUI 遮盖层：半透明遮罩 + 旋转指示器 + 提示文字，可挂在任意控件或窗体上。")]
public class SuLoadingOverlay : SuControlBase
{
    private static readonly ConditionalWeakTable<Control, SuLoadingOverlay> Cache = new();

    private readonly SuSpinnerAnimator _spinner;
    private readonly SuTween _fade;
    private Bitmap? _backdrop;
    private Control? _host;
    private string _loadingText = "加载中…";
    private int _spinnerSize = 34;
    private float _spinnerThickness = 3.2f;
    private Color? _scrimColor;
    private bool _showCard = true;
    private bool _busy;

    public SuLoadingOverlay()
    {
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        Dock = DockStyle.Fill;
        Cursor = Cursors.Default;

        _spinner = new SuSpinnerAnimator();
        _spinner.Tick += (_, __) => Invalidate();

        _fade = new SuTween();
        _fade.Changed += (_, __) => Invalidate();

        // 放到最后：Visible 变化会触发 OnVisibleChanged，此时字段必须已经就绪
        Visible = false;
    }

    /// <summary>提示文字。</summary>
    [Category("SuperUI")]
    [DefaultValue("加载中…")]
    public string LoadingText
    {
        get => _loadingText;
        set
        {
            value ??= string.Empty;
            if (_loadingText == value)
            {
                return;
            }

            _loadingText = value;
            Invalidate();
        }
    }

    /// <summary>指示器直径。</summary>
    [Category("SuperUI")]
    [DefaultValue(34)]
    public int SpinnerSize
    {
        get => _spinnerSize;
        set
        {
            var size = Math.Max(12, value);
            if (_spinnerSize == size)
            {
                return;
            }

            _spinnerSize = size;
            Invalidate();
        }
    }

    /// <summary>指示器线宽。</summary>
    [Category("SuperUI")]
    [DefaultValue(3.2f)]
    public float SpinnerThickness
    {
        get => _spinnerThickness;
        set
        {
            var thickness = Math.Max(1f, value);
            if (Math.Abs(_spinnerThickness - thickness) < 0.01f)
            {
                return;
            }

            _spinnerThickness = thickness;
            Invalidate();
        }
    }

    /// <summary>遮罩颜色；null 表示由主题背景色加透明推导。</summary>
    [Category("SuperUI")]
    [Description("遮罩颜色，null 表示由主题背景色推导（深色约 75% 不透明）。")]
    public Color? ScrimColor
    {
        get => _scrimColor;
        set
        {
            if (_scrimColor == value)
            {
                return;
            }

            _scrimColor = value;
            Invalidate();
        }
    }

    /// <summary>是否在指示器后面画一个圆角卡片。</summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    public bool ShowCard
    {
        get => _showCard;
        set
        {
            if (_showCard == value)
            {
                return;
            }

            _showCard = value;
            Invalidate();
        }
    }

    /// <summary>当前是否处于加载状态。</summary>
    [Browsable(false)]
    public bool IsLoading => _busy;

    /// <summary>把遮罩挂到目标控件（自动铺满、置于最上层、默认隐藏）。</summary>
    public void Attach(Control host)
    {
        if (host is null)
        {
            throw new ArgumentNullException(nameof(host));
        }

        _host = host;
        Dock = DockStyle.Fill;
        host.Controls.Add(this);
        BringToFront();
        Visible = false;
    }

    /// <summary>开始显示遮罩（已显示时只更新文字）。</summary>
    public void ShowLoading(string? text = null)
    {
        if (text is not null)
        {
            LoadingText = text;
        }

        if (_busy)
        {
            Invalidate();
            return;
        }

        _busy = true;
        CaptureBackdrop();

        Visible = true;
        BringToFront();
        _fade.Snap(0f);
        _fade.To(1f, 140);
        _spinner.Start();
        SuInputBlocker.Install(_host);
        Invalidate();

        ScheduleCapture();
    }

    /// <summary>立即重新截取目标区域（内容变化、需要刷新遮罩底图时调用）。</summary>
    public void RefreshBackdrop()
    {
        if (!_busy)
        {
            return;
        }

        CaptureBackdrop();
        Invalidate();
    }

    /// <summary>句柄/布局就绪后再截一次图：构造期调用 ShowLoading 时目标往往还没有句柄。</summary>
    private void ScheduleCapture()
    {
        if (!IsHandleCreated || _backdrop is not null)
        {
            return;
        }
        try
        {
            BeginInvoke(new Action(() =>
            {
                if (_busy && _backdrop is null)
                {
                    CaptureBackdrop();
                    Invalidate();
                }
            }));
        }
        catch (InvalidOperationException)
        {
            // 控件正在销毁，忽略。
        }
    }

    /// <summary>结束加载并隐藏遮罩。</summary>
    public void HideLoading()
    {
        if (!_busy)
        {
            Visible = false;
            return;
        }

        _busy = false;
        _spinner.Stop();
        _fade.Snap(0f);
        SuInputBlocker.Uninstall();
        Visible = false;
        DisposeBackdrop();
    }

    /// <summary>取（或创建）挂在目标控件上的遮罩。</summary>
    public static SuLoadingOverlay For(Control host)
    {
        if (host is null)
        {
            throw new ArgumentNullException(nameof(host));
        }

        if (Cache.TryGetValue(host, out var existing) && !existing.IsDisposed)
        {
            return existing;
        }

        var overlay = new SuLoadingOverlay();
        Cache.Remove(host);
        Cache.Add(host, overlay);
        overlay.Attach(host);
        return overlay;
    }

    /// <summary>在目标控件上显示遮罩。</summary>
    public static SuLoadingOverlay Show(Control host, string? text = null)
    {
        var overlay = For(host);
        overlay.ShowLoading(text);
        return overlay;
    }

    /// <summary>隐藏目标控件上的遮罩。</summary>
    public static void Hide(Control host)
    {
        if (host is not null && Cache.TryGetValue(host, out var overlay) && !overlay.IsDisposed)
        {
            overlay.HideLoading();
        }
    }

    /// <inheritdoc />
    public override void OnThemeChanged()
    {
        ApplyTheme();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void ApplyTheme()
    {
        base.ApplyTheme();
        BackColor = SuBackdrop.Resolve(this);
    }

    private void CaptureBackdrop()
    {
        DisposeBackdrop();

        if (_host is null || !_host.IsHandleCreated || _host.Width <= 0 || _host.Height <= 0)
        {
            return;
        }

        try
        {
            var bitmap = new Bitmap(_host.Width, _host.Height);
            _host.DrawToBitmap(bitmap, new Rectangle(0, 0, _host.Width, _host.Height));
            _backdrop = bitmap;
        }
        catch (Exception)
        {
            // 极少数控件（或绘制中的控件）无法快照，退化为纯色遮罩。
            _backdrop = null;
        }
    }

    private void DisposeBackdrop()
    {
        _backdrop?.Dispose();
        _backdrop = null;
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

        // 1) 目标区域快照（保证遮罩半透明时能透出内容）
        if (_backdrop is not null)
        {
            var offset = Location;
            graphics.DrawImage(
                _backdrop,
                new Rectangle(-offset.X, -offset.Y, _backdrop.Width, _backdrop.Height));
        }

        // 2) 半透明遮罩
        var scrim = _scrimColor ?? Color.FromArgb(theme.IsDark ? 200 : 210, theme.Background);
        var alpha = (int)(scrim.A * Math.Max(0f, _fade.Value));
        if (alpha > 0)
        {
            using var brush = new SolidBrush(SuDraw.Alpha(Color.FromArgb(255, scrim), alpha));
            graphics.FillRectangle(brush, ClientRectangle);
        }

        if (_fade.Value <= 0.01f)
        {
            return;
        }

        // 3) 指示器 + 文字（可带卡片底板）
        var spinnerSize = Math.Min(_spinnerSize, Math.Max(12, Math.Min(Width, Height) - 16));
        var hasText = !string.IsNullOrEmpty(_loadingText);
        var textHeight = hasText ? Font.Height : 0;
        var gap = hasText ? 12 : 0;
        var cardPaddingX = 28;
        var cardPaddingY = 22;

        var contentWidth = Math.Max(spinnerSize, hasText ? MeasureText(_loadingText) : 0);
        var contentHeight = spinnerSize + gap + textHeight;

        var cardWidth = contentWidth + cardPaddingX * 2;
        var cardHeight = contentHeight + cardPaddingY * 2;
        var cardRect = new RectangleF(
            (Width - cardWidth) / 2f,
            (Height - cardHeight) / 2f,
            cardWidth,
            cardHeight);

        if (_showCard)
        {
            var radius = Math.Max(6, theme.Radius + 4);
            using var cardPath = SuDraw.RoundedRect(cardRect, radius);
            using (var cardBrush = new SolidBrush(SuDraw.Alpha(theme.Surface, (int)(245 * _fade.Value))))
            {
                graphics.FillPath(cardBrush, cardPath);
            }

            using var cardPen = new Pen(SuDraw.Alpha(theme.Border, (int)(220 * _fade.Value)), 1f);
            graphics.DrawPath(cardPen, cardPath);
        }

        var spinnerRect = new RectangleF(
            Width / 2f - spinnerSize / 2f,
            cardRect.Top + cardPaddingY,
            spinnerSize,
            spinnerSize);

        SuSpinner.Draw(
            graphics,
            spinnerRect,
            _spinner.Angle,
            SuDraw.Alpha(theme.Primary, (int)(255 * _fade.Value)),
            _spinnerThickness,
            SuDraw.Alpha(theme.BorderStrong, (int)(110 * _fade.Value)));

        if (hasText)
        {
            var textRect = new Rectangle(
                0,
                (int)(spinnerRect.Bottom + gap),
                Width,
                textHeight + 4);

            TextRenderer.DrawText(
                graphics,
                _loadingText,
                Font,
                textRect,
                SuDraw.Alpha(theme.TextPrimary, (int)(255 * _fade.Value)),
                TextFormatFlags.HorizontalCenter
                | TextFormatFlags.Top
                | TextFormatFlags.NoPadding
                | TextFormatFlags.NoPrefix);
        }
    }

    private int MeasureText(string text)
        => TextRenderer.MeasureText(
            text,
            Font,
            new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width;

    /// <inheritdoc />
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);

        if (_spinner is null || _fade is null)
        {
            return;
        }

        if (Visible)
        {
            // 父容器首次可见时句柄才创建，这里补一次快照
            ScheduleCapture();
        }
        else if (!_busy)
        {
            _spinner.Stop();
        }
        else if (Parent is null || Parent.Visible)
        {
            // 真正被隐藏（不是父容器还没显示）：收尾
            _busy = false;
            _spinner.Stop();
            SuInputBlocker.Uninstall();
            DisposeBackdrop();
        }
    }

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        if (_busy)
        {
            ScheduleCapture();
        }
    }

    /// <inheritdoc />
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();

        if (_busy)
        {
            CaptureBackdrop();
        }
    }

    /// <inheritdoc />
    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        // 背景完全由 OnPaint 负责（需要叠加快照与半透明遮罩）
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SuInputBlocker.Uninstall();
            DisposeBackdrop();
            _spinner.Dispose();
            _fade.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>显示遮罩期间吞掉目标区域内的键盘 / 滚轮消息。</summary>
    private sealed class SuInputBlocker : IMessageFilter
    {
        private const int WmKeyDown = 0x0100;
        private const int WmKeyUp = 0x0101;
        private const int WmChar = 0x0102;
        private const int WmSysKeyDown = 0x0104;
        private const int WmSysKeyUp = 0x0105;
        private const int WmMouseWheel = 0x020A;

        private static SuInputBlocker? _current;

        private readonly Control? _host;

        private SuInputBlocker(Control? host) => _host = host;

        public static void Install(Control? host)
        {
            Uninstall();

            if (host is null)
            {
                return;
            }

            _current = new SuInputBlocker(host);
            Application.AddMessageFilter(_current);
        }

        public static void Uninstall()
        {
            if (_current is null)
            {
                return;
            }

            Application.RemoveMessageFilter(_current);
            _current = null;
        }

        public bool PreFilterMessage(ref Message m)
        {
            switch (m.Msg)
            {
                case WmKeyDown:
                case WmKeyUp:
                case WmChar:
                case WmSysKeyDown:
                case WmSysKeyUp:
                case WmMouseWheel:
                    break;
                default:
                    return false;
            }

            var control = Control.FromHandle(m.HWnd);
            if (control is null)
            {
                return false;
            }

            for (var current = control; current is not null; current = current.Parent)
            {
                if (ReferenceEquals(current, _host))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
