using System.ComponentModel;
using System.Drawing.Drawing2D;
using SuperUI.Native;
using SuperUI.Rendering;
using SuperUI.Theming;

namespace SuperUI.Forms;

/// <summary>标题栏上的系统按钮。</summary>
public enum SuCaptionButton
{
    /// <summary>标题 / 拖动区域（非按钮）。</summary>
    None,

    /// <summary>最小化。</summary>
    Minimize,

    /// <summary>最大化 / 还原。</summary>
    MaximizeRestore,

    /// <summary>关闭。</summary>
    Close,
}

/// <summary>
/// 无边框窗口的自绘标题栏：标题、Logo、最小化 / 最大化 / 关闭按钮，颜色随全局主题切换。
/// 由 <see cref="SuBorderlessForm"/> 内部创建，也可以单独使用。
/// </summary>
[Description("SuperUI 自绘标题栏：标题 + 窗口按钮，颜色跟随全局主题。")]
public class SuTitleBar : Control, ISuThemed
{
    private string _title = "SuperUI";
    private Image? _logo;
    private bool _showMinimizeBox = true;
    private bool _showMaximizeBox = true;
    private bool _showCloseBox = true;
    private bool _isMaximized;
    private int _captionButtonWidth = 46;
    private SuCaptionButton _hoveredButton;
    private SuCaptionButton _pressedButton;
    private int _lastClickTicks;
    private Point _lastClickPoint;
    private int _appliedThemeHeight;
    private readonly SuFontSync _fontSync;

    public SuTitleBar()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);

        _fontSync = new SuFontSync(this);
        Height = 24;
        Dock = DockStyle.Top;
        BackColor = SuThemeManager.Current.TitleBarBackground;
        SuThemeManager.Register(this);
        ApplyTheme();
    }

    /// <summary>最小化按钮点击。</summary>
    public event EventHandler? MinimizeRequested;

    /// <summary>最大化 / 还原按钮点击。</summary>
    public event EventHandler? MaximizeRestoreRequested;

    /// <summary>关闭按钮点击。</summary>
    public event EventHandler? CloseRequested;

    /// <summary>标题文字。</summary>
    [Category("SuperUI")]
    [DefaultValue("SuperUI")]
    public string Title
    {
        get => _title;
        set
        {
            value ??= string.Empty;
            if (_title == value)
            {
                return;
            }

            _title = value;
            Invalidate();
        }
    }

    /// <summary>左侧 Logo（为 null 时绘制主色方块 + 标题首字）。</summary>
    [Category("SuperUI")]
    [DefaultValue(null)]
    public Image? Logo
    {
        get => _logo;
        set
        {
            _logo = value;
            Invalidate();
        }
    }

    /// <summary>是否显示最小化按钮。</summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    public bool ShowMinimizeBox
    {
        get => _showMinimizeBox;
        set
        {
            _showMinimizeBox = value;
            Invalidate();
        }
    }

    /// <summary>是否显示最大化 / 还原按钮。</summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    public bool ShowMaximizeBox
    {
        get => _showMaximizeBox;
        set
        {
            _showMaximizeBox = value;
            Invalidate();
        }
    }

    /// <summary>是否显示关闭按钮。</summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    public bool ShowCloseBox
    {
        get => _showCloseBox;
        set
        {
            _showCloseBox = value;
            Invalidate();
        }
    }

    /// <summary>窗口当前是否最大化（决定绘制“最大化”还是“还原”图标）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsMaximized
    {
        get => _isMaximized;
        set
        {
            if (_isMaximized == value)
            {
                return;
            }

            _isMaximized = value;
            Invalidate();
        }
    }

    /// <summary>单个窗口按钮的宽度。</summary>
    [Category("SuperUI")]
    [DefaultValue(46)]
    public int CaptionButtonWidth
    {
        get => _captionButtonWidth;
        set
        {
            _captionButtonWidth = Math.Max(20, value);
            Invalidate();
        }
    }

    /// <summary>
    /// 顶部缩放热区高度：点击标题栏最上方这么多像素时执行上边缘缩放，其余位置拖动窗口。
    /// </summary>
    [Category("SuperUI")]
    [DefaultValue(6)]
    public int TopResizeBand { get; set; } = 6;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SuTheme Theme => SuThemeManager.Current;

    /// <inheritdoc />
    public virtual void OnThemeChanged()
    {
        ApplyTheme();
        Invalidate();
    }

    /// <summary>判断某个客户区坐标是否落在窗口按钮上。</summary>
    public SuCaptionButton HitTest(Point clientPoint)
    {
        if (!ClientRectangle.Contains(clientPoint))
        {
            return SuCaptionButton.None;
        }

        var index = 0;
        foreach (var button in EnumerateVisibleButtons())
        {
            var left = Width - (index + 1) * _captionButtonWidth;
            if (clientPoint.X >= left && clientPoint.X < left + _captionButtonWidth)
            {
                return button;
            }

            index++;
        }

        return SuCaptionButton.None;
    }

    /// <summary>该点是否属于可拖动区域（非按钮）。</summary>
    public bool IsDragArea(Point clientPoint) => HitTest(clientPoint) == SuCaptionButton.None;

    private IEnumerable<SuCaptionButton> EnumerateVisibleButtons()
    {
        if (_showCloseBox)
        {
            yield return SuCaptionButton.Close;
        }

        if (_showMaximizeBox)
        {
            yield return SuCaptionButton.MaximizeRestore;
        }

        if (_showMinimizeBox)
        {
            yield return SuCaptionButton.Minimize;
        }
    }

    private void ApplyTheme()
    {
        _fontSync.Apply(Theme);
        BackColor = Theme.TitleBarBackground;
        ForeColor = Theme.TitleBarText;

        // 主题里的标题栏高度只在用户没有手动改过高度时生效。
        var themeHeight = Theme.TitleBarHeight;
        if (themeHeight > 0 && (Height == _appliedThemeHeight || _appliedThemeHeight == 0))
        {
            Height = themeHeight;
        }

        _appliedThemeHeight = themeHeight;
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        SuDraw.SetupQuality(graphics);

        var theme = Theme;
        using (var background = new SolidBrush(theme.TitleBarBackground))
        {
            graphics.FillRectangle(background, ClientRectangle);
        }

        var textLeft = DrawLogo(graphics, theme);
        DrawTitle(graphics, theme, textLeft);
        DrawCaptionButtons(graphics, theme);

        using var borderPen = new Pen(theme.Border);
        graphics.DrawLine(borderPen, 0, Height - 1, Width, Height - 1);
    }

    private int DrawLogo(Graphics graphics, SuTheme theme)
    {
        const int size = 20;
        var top = (Height - 1 - size) / 2;
        var rect = new Rectangle(14, top, size, size);

        if (_logo is not null)
        {
            graphics.DrawImage(_logo, rect);
            return rect.Right + 10;
        }

        using var path = SuDraw.RoundedRect(rect, 5f);
        using (var brush = new LinearGradientBrush(rect, theme.Primary, theme.PrimaryHover, LinearGradientMode.ForwardDiagonal))
        {
            graphics.FillPath(brush, path);
        }

        var initial = string.IsNullOrEmpty(_title) ? "S" : _title.Substring(0, 1);
        TextRenderer.DrawText(
            graphics,
            initial,
            Font,
            rect,
            theme.TextOnAccent,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

        return rect.Right + 10;
    }

    private void DrawTitle(Graphics graphics, SuTheme theme, int left)
    {
        var right = Width;
        foreach (var _ in EnumerateVisibleButtons())
        {
            right -= _captionButtonWidth;
        }

        var bounds = new Rectangle(left, 0, Math.Max(0, right - left - 12), Height - 1);
        if (bounds.Width <= 0)
        {
            return;
        }

        TextRenderer.DrawText(
            graphics,
            _title,
            Font,
            bounds,
            theme.TitleBarText,
            TextFormatFlags.Left
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis
            | TextFormatFlags.NoPadding
            | TextFormatFlags.NoPrefix);
    }

    private void DrawCaptionButtons(Graphics graphics, SuTheme theme)
    {
        var index = 0;
        foreach (var button in EnumerateVisibleButtons())
        {
            var bounds = new Rectangle(Width - (index + 1) * _captionButtonWidth, 0, _captionButtonWidth, Height - 1);
            var isHovered = _hoveredButton == button;
            var isPressed = _pressedButton == button;

            if (isHovered || isPressed)
            {
                var hoverColor = button == SuCaptionButton.Close ? theme.CaptionCloseHover : theme.CaptionHover;
                if (isPressed)
                {
                    hoverColor = SuDraw.Darken(hoverColor, 0.12f);
                }

                using var brush = new SolidBrush(hoverColor);
                graphics.FillRectangle(brush, bounds);
            }

            var glyphColor = (isHovered || isPressed) && button == SuCaptionButton.Close
                ? theme.TextOnAccent
                : theme.TitleBarText;

            DrawGlyph(graphics, button, bounds, glyphColor, _isMaximized);
            index++;
        }
    }

    private static void DrawGlyph(Graphics graphics, SuCaptionButton button, Rectangle bounds, Color color, bool isMaximized)
    {
        var cx = bounds.Left + bounds.Width / 2f;
        var cy = bounds.Top + bounds.Height / 2f;

        using var pen = new Pen(color, 1.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        switch (button)
        {
            case SuCaptionButton.Minimize:
                graphics.DrawLine(pen, cx - 5f, cy, cx + 5f, cy);
                break;

            case SuCaptionButton.MaximizeRestore when isMaximized:
                // 还原图标：两个错位方框
                graphics.DrawRectangle(pen, cx - 2.5f, cy - 5.5f, 7f, 7f);
                graphics.DrawRectangle(pen, cx - 5.5f, cy - 2.5f, 7f, 7f);
                break;

            case SuCaptionButton.MaximizeRestore:
                graphics.DrawRectangle(pen, cx - 4.5f, cy - 4.5f, 9f, 9f);
                break;

            case SuCaptionButton.Close:
                graphics.DrawLine(pen, cx - 4.5f, cy - 4.5f, cx + 4.5f, cy + 4.5f);
                graphics.DrawLine(pen, cx + 4.5f, cy - 4.5f, cx - 4.5f, cy + 4.5f);
                break;
        }
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        var hit = HitTest(e.Location);
        if (hit != _hoveredButton)
        {
            _hoveredButton = hit;
            Cursor = hit == SuCaptionButton.None ? Cursors.Default : Cursors.Hand;
            Invalidate();
        }

        base.OnMouseMove(e);
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        if (_hoveredButton != SuCaptionButton.None)
        {
            _hoveredButton = SuCaptionButton.None;
            Cursor = Cursors.Default;
            Invalidate();
        }

        base.OnMouseLeave(e);
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            var button = HitTest(e.Location);
            if (button != SuCaptionButton.None)
            {
                _pressedButton = button;
                Invalidate();
            }
            else
            {
                // 双击标题空白处：最大化 / 还原。拖动会让系统进入模态移动循环，第二次点击收不到
                // WM_LBUTTONDBLCLK，所以这里自己按系统双击时间和距离判定。
                if (_showMaximizeBox && IsDoubleClick(e.Location))
                {
                    MaximizeRestoreRequested?.Invoke(this, EventArgs.Empty);
                    return;
                }

                // 交给系统进入标准移动 / 缩放循环：Aero Snap、拖动最大化窗口还原都走系统原生逻辑。
                var form = FindForm();
                if (form is not null && form.IsHandleCreated)
                {
                    var hitCode = e.Y <= TopResizeBand ? SuNative.HTTOP : SuNative.HTCAPTION;
                    SuNative.ReleaseCapture();

                    // 无边框窗口平时没有 WS_THICKFRAME，Windows 不会给它吸附手势：
                    // 拖动期间临时挂上（不改客户区、不画边框），拖到屏幕顶部就能最大化、拖到左右两侧就能半屏。
                    var snapForm = hitCode == SuNative.HTCAPTION ? form as SuBorderlessForm : null;
                    snapForm?.ArmSnapStyle();
                    try
                    {
                        SuNative.SendMessage(form.Handle, SuNative.WM_NCLBUTTONDOWN, hitCode, 0);
                    }
                    finally
                    {
                        snapForm?.DisarmSnapStyle();
                        snapForm?.FinishDragSnap();
                    }

                    return;
                }
            }
        }

        base.OnMouseDown(e);
    }

    /// <inheritdoc />
    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            var pressed = _pressedButton;
            _pressedButton = SuCaptionButton.None;

            if (pressed != SuCaptionButton.None && HitTest(e.Location) == pressed)
            {
                Raise(pressed);
            }

            Invalidate();
        }

        base.OnMouseUp(e);
    }

    /// <inheritdoc />
    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        if (HitTest(e.Location) == SuCaptionButton.MaximizeRestore && _showMaximizeBox)
        {
            MaximizeRestoreRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// 按系统双击时间 / 距离判断这次按下是否构成双击，并顺手记下本次按下的时间与位置。
    /// 判定成功后清空记录，避免三连击再触发一次。
    /// </summary>
    private bool IsDoubleClick(Point location)
    {
        var now = Environment.TickCount;
        var span = unchecked(now - _lastClickTicks);
        var size = SystemInformation.DoubleClickSize;
        var inRange = Math.Abs(location.X - _lastClickPoint.X) <= size.Width
                      && Math.Abs(location.Y - _lastClickPoint.Y) <= size.Height;

        var isDoubleClick = _lastClickTicks != 0
                            && span >= 0
                            && span <= SystemInformation.DoubleClickTime
                            && inRange;

        _lastClickTicks = isDoubleClick ? 0 : now;
        _lastClickPoint = location;
        return isDoubleClick;
    }

    private void Raise(SuCaptionButton button)
    {
        switch (button)
        {
            case SuCaptionButton.Minimize:
                MinimizeRequested?.Invoke(this, EventArgs.Empty);
                break;
            case SuCaptionButton.MaximizeRestore:
                MaximizeRestoreRequested?.Invoke(this, EventArgs.Empty);
                break;
            case SuCaptionButton.Close:
                CloseRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (_fontSync is null)
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
