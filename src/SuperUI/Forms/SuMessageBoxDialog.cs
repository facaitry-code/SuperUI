using System.ComponentModel;
using System.Drawing.Drawing2D;
using SuperUI.Controls;
using SuperUI.Native;
using SuperUI.Rendering;
using SuperUI.Theming;

namespace SuperUI.Forms;

/// <summary>弹窗语义：决定左侧图标的形状与配色。</summary>
public enum SuMessageBoxKind
{
    /// <summary>信息提示（主色圆形 i）。</summary>
    Info,

    /// <summary>警告（琥珀色三角 !）。</summary>
    Warning,

    /// <summary>错误（危险色圆形 ×）。</summary>
    Error,
}

/// <summary>弹窗按钮组合。</summary>
public enum SuMessageBoxButtons
{
    /// <summary>只有一个「确定」（Enter / Esc 都关窗）。</summary>
    Ok,

    /// <summary>「确定」+「取消」。</summary>
    OkCancel,

    /// <summary>「是」+「否」，返回 <see cref="DialogResult.Yes"/> / <see cref="DialogResult.No"/>。</summary>
    YesNo,
}

/// <summary>
/// SuperUI 消息弹窗：没有标题栏的圆角卡片，左侧一枚语义图标（信息 / 警告 / 错误），
/// 右边标题 + 自动换行的正文，底部右对齐的主题按钮；整窗可拖动、DWM 投影、入场淡入，
/// 默认还会在宿主窗口上盖一层整窗压暗的遮罩（见 <see cref="ShowScrim"/>）。
/// 颜色与字体全部取自 <see cref="SuTheme"/>，换主题即时刷新。
/// <para>
/// 一般不直接 new，用 <see cref="SuMessageBox.Info(string, string?, IWin32Window?)"/>、
/// <see cref="SuMessageBox.Warning(string, string?, IWin32Window?)"/>、
/// <see cref="SuMessageBox.Error(string, string?, IWin32Window?)"/> 更省事。
/// </para>
/// </summary>
/// <example>
/// <code>
/// using var box = new SuMessageBoxDialog
/// {
///     Kind = SuMessageBoxKind.Warning,
///     Text = "磁盘空间不足",                    // 窗口标题 + 弹窗标题
///     Message = "C 盘只剩 210 MB，清理后再试。",
///     ButtonSet = SuMessageBoxButtons.OkCancel,
///     ShowScrim = true,                         // 默认 true：宿主窗口整窗压暗
///     ScrimOpacity = 0.45f,                     // 压暗程度
/// };
/// box.ShowDialog(this);
/// </code>
/// </example>
[Description("SuperUI 消息弹窗：信息 / 警告 / 错误三种语义图标，无标题栏卡片、整窗可拖动、可选整窗遮罩，颜色跟随全局主题。")]
public class SuMessageBoxDialog : SuBorderlessForm
{
    private const int ContentPadding = 22;   // 内容四周留白
    private const int IconBox = 34;          // 图标外接方框
    private const int IconGap = 15;          // 图标与文字列间距
    private const int TitleGap = 7;          // 标题与正文间距
    private const int FooterGap = 22;        // 正文与按钮行间距
    private const int ButtonWidth = 88;
    private const int ButtonHeight = 36;
    private const int ButtonGap = 10;
    private const int MaxTextWidth = 384;    // 正文最大宽度，超出自动换行
    private const int MinTextWidth = 176;    // 正文最短宽度，避免出现竖条状窄窗

    private readonly List<SuButton> _actionButtons = new();
    private readonly SuTween _fade;

    private SuMessageBoxKind _kind = SuMessageBoxKind.Info;
    private SuMessageBoxButtons _buttonSet = SuMessageBoxButtons.Ok;
    private string _message = string.Empty;
    private Font? _titleFont;
    private Font? _bodyFont;
    private Size _titleSize;
    private Size _bodySize;
    private int _textWidth;
    private bool _ready;

    private bool _showScrim = true;
    private Color _scrimColor = Color.Black;
    private float _scrimOpacity = 0.45f;
    private SuScrimLayer? _scrim;

    public SuMessageBoxDialog()
    {
        // 弹窗不要窗口装饰：标题栏、缩放、任务栏按钮全部关掉，窗口本身就是一张卡片。
        TitleBarVisible = false;
        Resizable = false;
        ShowMinimizeBox = false;
        ShowMaximizeBox = false;
        ShowCloseBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;

        // 尺寸、留白、字号全部自己按 DeviceDpi 算，关掉自动缩放避免被放大两次。
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Theme.Surface;
        AccessibleRole = AccessibleRole.Dialog;

        _fade = new SuTween();
        _fade.Changed += (_, __) => Opacity = _fade.Value;
        Opacity = 0d;

        Text = SuMessageBox.DefaultTitle(SuMessageBoxKind.Info);

        ApplyThemeFaces();
        BuildButtons();
        _ready = true;
        Relayout();
    }

    /// <summary>弹窗语义，默认 <see cref="SuMessageBoxKind.Info"/>。</summary>
    [Category("SuperUI")]
    [DefaultValue(SuMessageBoxKind.Info)]
    [Description("弹窗语义：Info（主色 i）/ Warning（琥珀 !）/ Error（危险色 ×）。")]
    public SuMessageBoxKind Kind
    {
        get => _kind;
        set
        {
            if (_kind == value)
            {
                return;
            }

            _kind = value;
            Invalidate();
        }
    }

    /// <summary>按钮组合，默认 <see cref="SuMessageBoxButtons.Ok"/>。</summary>
    [Category("SuperUI")]
    [DefaultValue(SuMessageBoxButtons.Ok)]
    [Description("按钮组合：Ok / OkCancel / YesNo，改动后按需重建按钮并重排。")]
    public SuMessageBoxButtons ButtonSet
    {
        get => _buttonSet;
        set
        {
            if (_buttonSet == value)
            {
                return;
            }

            _buttonSet = value;
            BuildButtons();
            Relayout();
            Invalidate();
        }
    }

    /// <summary>正文（支持换行，按最大宽度自动换行，窗口高度随之变化）。</summary>
    [Category("SuperUI")]
    [DefaultValue("")]
    [Description("正文内容，支持 \\r\\n 换行，超宽自动换行并撑高窗口。")]
    public string Message
    {
        get => _message;
        set
        {
            value ??= string.Empty;
            if (_message == value)
            {
                return;
            }

            _message = value;
            Relayout();
            Invalidate();
        }
    }

    /// <summary>标题字号（pt），默认比主题字号大 1.5pt。</summary>
    [Category("SuperUI")]
    [Description("标题字号（pt）。")]
    public float TitleFontSize => Theme.FontSize + 1.5f;

    /// <summary>正文字号（pt），默认比主题字号大 0.5pt。</summary>
    [Category("SuperUI")]
    [Description("正文字号（pt）。")]
    public float BodyFontSize => Theme.FontSize + 0.5f;

    /// <summary>
    /// 是否在宿主窗口上盖一层遮罩（整窗压暗），默认 <c>true</c>。
    /// 遮罩只有在能拿到宿主控件时才出现：<see cref="ScrimHost"/> 或 <see cref="Form.Owner"/>。
    /// </summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    [Description("是否在宿主窗口上盖一层整窗压暗的遮罩。没有宿主（无 owner）时自动不显示。")]
    public bool ShowScrim
    {
        get => _showScrim;
        set
        {
            if (_showScrim == value)
            {
                return;
            }

            _showScrim = value;

            if (!Visible)
            {
                return;   // 还没显示，交给 OnShown
            }

            if (value)
            {
                ShowScrimLayer();
            }
            else
            {
                DismissScrim();
            }
        }
    }

    /// <summary>遮罩颜色，默认黑色。</summary>
    [Category("SuperUI")]
    [Description("遮罩颜色，默认黑色（配合 ScrimOpacity 决定压暗程度）。")]
    public Color ScrimColor
    {
        get => _scrimColor;
        set
        {
            if (_scrimColor == value)
            {
                return;
            }

            _scrimColor = value;
            if (_scrim is not null)
            {
                _scrim.ScrimColor = value;
            }
        }
    }

    /// <summary>遮罩不透明度（0..1），默认 0.45。</summary>
    [Category("SuperUI")]
    [DefaultValue(0.45f)]
    [Description("遮罩不透明度：0 = 不变暗，1 = 全黑。默认 0.45。")]
    public float ScrimOpacity
    {
        get => _scrimOpacity;
        set
        {
            var opacity = SuMath.Clamp(value, 0f, 1f);
            if (Math.Abs(_scrimOpacity - opacity) < 0.001f)
            {
                return;
            }

            _scrimOpacity = opacity;
            if (_scrim is not null)
            {
                _scrim.Opacity = opacity;
            }
        }
    }

    /// <summary>
    /// 遮罩盖在哪个控件上。<c>null</c> 时自动取 <see cref="Form.Owner"/>——
    /// <see cref="SuMessageBox.Show(string, SuMessageBoxKind, string?, SuMessageBoxButtons, IWin32Window?, bool?)"/>
    /// 会把它设成传进来的 owner，所以走静态入口不用管这个属性。
    /// </summary>
    [Category("SuperUI")]
    [DefaultValue(null)]
    [Description("遮罩挂在哪个控件上；null 表示自动取 Owner（弹窗的父窗口）。")]
    public Control? ScrimHost { get; set; }

    /// <inheritdoc />
    /// <remarks>整窗拖动：卡片没有标题栏，所以客户区里非按钮的区域都按标题栏（HTCAPTION）处理。</remarks>
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == SuNative.WM_NCHITTEST)
        {
            base.WndProc(ref m);
            if ((int)m.Result == SuNative.HTCLIENT
                && !IsOverActionButton(PointToClient(SuNative.PointFromLParam(m.LParam))))
            {
                m.Result = (IntPtr)SuNative.HTCAPTION;
            }

            return;
        }

        if (m.Msg == SuNative.WM_NCLBUTTONDBLCLK)
        {
            // 双击卡片不该最大化（卡片本来就是固定尺寸）。
            return;
        }

        base.WndProc(ref m);
    }

    /// <inheritdoc />
    public override void OnThemeChanged()
    {
        base.OnThemeChanged();

        BackColor = Theme.Surface;
        ApplyThemeFaces();
        Relayout();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Relayout();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        // 先挂遮罩再淡入：遮罩与卡片同时出现，视觉上是一次"浮起"。
        ShowScrimLayer();

        // 入场淡入：120ms 内 0 → 1，结束后 Opacity = 1，DWM 圆角与投影重新生效。
        _fade.Snap(0f);
        Opacity = 0d;
        _fade.To(1f, 120);
    }

    /// <inheritdoc />
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        // 遮罩必须比宿主先撤：宿主一旦恢复可交互，压在它上面的半透明层就成了"点不动的盖子"。
        DismissScrim();
        base.OnFormClosed(e);
    }

    /// <summary>按当前设置把遮罩挂到宿主上（已存在 / 没有宿主 / 关闭了遮罩时什么都不做）。</summary>
    private void ShowScrimLayer()
    {
        if (!_showScrim || _scrim is not null)
        {
            return;
        }

        var host = ScrimHost ?? Owner as Control ?? ResolveOwnerWindow();
        if (host is null)
        {
            return;
        }

        _scrim = SuScrimLayer.Show(host, _scrimColor, _scrimOpacity);
    }

    /// <summary>
    /// 从 Win32 owner 取宿主控件：<see cref="Form.ShowDialog(IWin32Window)"/> 只登记了窗口宿主关系，
    /// 不一定给 <see cref="Form.Owner"/> 赋值，所以直接 new 弹窗的用法要靠这里兜住。
    /// </summary>
    private Control? ResolveOwnerWindow()
    {
        if (!IsHandleCreated)
        {
            return null;
        }

        var owner = SuNative.GetWindow(Handle, SuNative.GW_OWNER);
        return owner == IntPtr.Zero ? null : Control.FromHandle(owner);
    }

    private void DismissScrim()
    {
        if (_scrim is null)
        {
            return;
        }

        _scrim.Dismiss();
        _scrim.Dispose();
        _scrim = null;
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        SuDraw.SetupQuality(graphics);

        var theme = Theme;

        // 双缓冲缓冲池会复用上一帧像素：圆角外与卡片底都要显式铺满。
        using (var background = new SolidBrush(theme.Surface))
        {
            graphics.FillRectangle(background, ClientRectangle);
        }

        if (!_ready)
        {
            base.OnPaint(e);
            return;
        }

        var pad = Scale(ContentPadding);
        var iconRect = new RectangleF(pad, pad, Scale(IconBox), Scale(IconBox));
        DrawMark(graphics, iconRect, theme);

        var textLeft = (int)Math.Round(iconRect.Right + Scale(IconGap));
        var top = (float)pad;

        if (_titleFont is not null && !string.IsNullOrEmpty(Text))
        {
            TextRenderer.DrawText(
                graphics,
                Text,
                _titleFont,
                new Rectangle(textLeft, (int)top, _textWidth, _titleSize.Height),
                theme.TextPrimary,
                TextFormatFlags.NoPrefix
                | TextFormatFlags.NoPadding
                | TextFormatFlags.SingleLine
                | TextFormatFlags.EndEllipsis);

            top += _titleSize.Height + Scale(TitleGap);
        }

        if (_bodyFont is not null && !string.IsNullOrEmpty(_message))
        {
            TextRenderer.DrawText(
                graphics,
                _message,
                _bodyFont,
                new Rectangle(textLeft, (int)Math.Round(top), _textWidth, _bodySize.Height),
                theme.TextPrimary,
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);
        }

        // 基类画 1px 外框（BorderStrong），放在最后才不会被子控件的底色盖住。
        base.OnPaint(e);
    }

    /// <summary>点是否落在按钮上：落在按钮上要交回 HTCLIENT，否则按钮点不动（会被当成拖动标题栏）。</summary>
    private bool IsOverActionButton(Point clientPoint)
    {
        foreach (var button in _actionButtons)
        {
            if (button.Visible && button.Bounds.Contains(clientPoint))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>按当前字体、正文、按钮组合重新量尺寸并重排：窗口宽度跟着最宽的一行走。</summary>
    private void Relayout()
    {
        if (!_ready)
        {
            return;
        }

        var pad = Scale(ContentPadding);
        var icon = Scale(IconBox);
        var iconGap = Scale(IconGap);
        var titleGap = Scale(TitleGap);
        var footerGap = Scale(FooterGap);
        var buttonWidth = Scale(ButtonWidth);
        var buttonHeight = Scale(ButtonHeight);
        var buttonGap = Scale(ButtonGap);
        var maxTextWidth = Scale(MaxTextWidth);
        var minTextWidth = Scale(MinTextWidth);

        var hasHeading = !string.IsNullOrEmpty(Text) && _titleFont is not null;

        _titleSize = hasHeading
            ? Measure(Text, _titleFont!, maxTextWidth, singleLine: true)
            : Size.Empty;

        _bodySize = _bodyFont is null
            ? Size.Empty
            : Measure(_message, _bodyFont, maxTextWidth, singleLine: false);

        _textWidth = Math.Max(minTextWidth, _bodySize.Width);
        if (hasHeading)
        {
            _textWidth = Math.Max(_textWidth, _titleSize.Width);
        }

        var textHeight = _bodySize.Height + (hasHeading ? _titleSize.Height + titleGap : 0);
        var contentHeight = Math.Max(icon, textHeight);
        var contentWidth = icon + iconGap + _textWidth;

        var rowWidth = _actionButtons.Count * buttonWidth
                       + Math.Max(0, _actionButtons.Count - 1) * buttonGap;

        var width = Math.Max(pad * 2 + contentWidth, pad * 2 + rowWidth);
        var height = pad + contentHeight + footerGap + buttonHeight + pad;

        ApplyLockedSize(new Size(width, height));

        // 按钮行贴右下角，最后一个（确认类）按钮在最右。
        var top = height - pad - buttonHeight;
        var left = width - pad - rowWidth;
        foreach (var button in _actionButtons)
        {
            button.SetBounds(left, top, buttonWidth, buttonHeight);
            left += buttonWidth + buttonGap;
        }
    }

    /// <summary>
    /// 锁定客户区尺寸。<see cref="SuBorderlessForm.Resizable"/> 为 false 时基类会把
    /// <see cref="Control.MaximumSize"/> 锁成当时的尺寸，所以这里先解锁、改尺寸、再锁回去。
    /// </summary>
    private void ApplyLockedSize(Size size)
    {
        MaximumSize = Size.Empty;
        MinimumSize = Size.Empty;

        if (ClientSize != size)
        {
            ClientSize = size;
        }

        MinimumSize = size;
        MaximumSize = size;
    }

    /// <summary>测量文本；标题是单行（超宽用省略号收尾），正文按最大宽度换行。</summary>
    private Size Measure(string text, Font font, int maxWidth, bool singleLine)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Size.Empty;
        }

        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        if (singleLine)
        {
            flags |= TextFormatFlags.SingleLine;
        }
        else
        {
            flags |= TextFormatFlags.WordBreak;
        }

        var measured = TextRenderer.MeasureText(text, font, new Size(maxWidth, int.MaxValue), flags);
        return new Size(Math.Min(measured.Width, maxWidth), measured.Height);
    }

    private void ApplyThemeFaces()
    {
        var titleFont = Theme.CreateFont(FontStyle.Bold, TitleFontSize);
        var bodyFont = Theme.CreateFont(FontStyle.Regular, BodyFontSize);

        _titleFont?.Dispose();
        _bodyFont?.Dispose();
        _titleFont = titleFont;
        _bodyFont = bodyFont;
    }

    /// <summary>按当前 <see cref="ButtonSet"/> 重建底部按钮；「确定」是默认按钮（Enter）。</summary>
    private void BuildButtons()
    {
        foreach (var button in _actionButtons)
        {
            Controls.Remove(button);
            button.Dispose();
        }

        _actionButtons.Clear();
        AcceptButton = null;
        CancelButton = null;

        switch (_buttonSet)
        {
            case SuMessageBoxButtons.OkCancel:
                AddActionButton("取消", SuButtonVariant.Secondary, DialogResult.Cancel, accept: false);
                AddActionButton("确定", SuButtonVariant.Primary, DialogResult.OK, accept: true);
                break;

            case SuMessageBoxButtons.YesNo:
                AddActionButton("否", SuButtonVariant.Secondary, DialogResult.No, accept: false);
                AddActionButton("是", SuButtonVariant.Primary, DialogResult.Yes, accept: true);
                break;

            default:
                // 只有一个「确定」时，Esc 也当作确认关窗（和系统 MessageBox 的 Esc 行为一致）。
                var ok = AddActionButton("确定", SuButtonVariant.Primary, DialogResult.OK, accept: true);
                CancelButton = ok;
                break;
        }
    }

    private SuButton AddActionButton(string text, SuButtonVariant variant, DialogResult result, bool accept)
    {
        var button = new SuButton
        {
            Text = text,
            Variant = variant,
            DialogResult = result,
        };

        Controls.Add(button);
        _actionButtons.Add(button);

        if (accept)
        {
            AcceptButton = button;
        }
        else
        {
            CancelButton ??= button;
        }

        return button;
    }

    /// <summary>像素常量按当前 DPI 换算（AutoScaleMode 已关，这里是唯一的缩放来源）。</summary>
    private int Scale(int value) => (int)Math.Round(value * DeviceDpi / 96.0);

    /// <summary>画语义图标：底下一层淡色形状，上面一枚同色标记。</summary>
    private void DrawMark(Graphics graphics, RectangleF box, SuTheme theme)
    {
        var color = _kind switch
        {
            SuMessageBoxKind.Warning => theme.Warning,
            SuMessageBoxKind.Error => theme.Danger,
            _ => theme.Primary,
        };

        // 图标底色：把标记色淡淡混进窗体表面色（深色主题压得更深，标记色才跳得出来），
        // 外面再套一圈同色细边——34px 的小图标靠这圈边才立得住。
        var soft = SuDraw.Blend(theme.Surface, color, theme.IsDark ? 0.22f : 0.16f);
        var ring = SuDraw.Alpha(color, theme.IsDark ? 150 : 95);

        using var softBrush = new SolidBrush(soft);
        using var ringPen = new Pen(ring, 1.3f) { LineJoin = LineJoin.Round };

        if (_kind == SuMessageBoxKind.Warning)
        {
            // 警告用圆角三角：填充 + 细描边（LineJoin.Round 把三个尖角磨圆）。
            using var path = TrianglePath(box);
            graphics.FillPath(softBrush, path);
            graphics.DrawPath(ringPen, path);
            DrawMarkBang(graphics, box, color);
            return;
        }

        // 圆形底：矩形内缩半个笔宽，描边才完整落在方框里。
        var circle = new RectangleF(box.Left + 0.65f, box.Top + 0.65f, box.Width - 1.3f, box.Height - 1.3f);
        graphics.FillEllipse(softBrush, circle);
        graphics.DrawEllipse(ringPen, circle);

        if (_kind == SuMessageBoxKind.Error)
        {
            DrawMarkCross(graphics, box, color);
        }
        else
        {
            DrawMarkInfo(graphics, box, color);
        }
    }

    /// <summary>圆角三角形（顶点在上，底边在下）。</summary>
    private static GraphicsPath TrianglePath(RectangleF box)
    {
        var inset = box.Width * 0.04f;
        var path = new GraphicsPath();
        path.AddPolygon(new[]
        {
            new PointF(box.Left + box.Width / 2f, box.Top + inset),
            new PointF(box.Right - inset, box.Bottom - inset),
            new PointF(box.Left + inset, box.Bottom - inset),
        });
        path.CloseFigure();
        return path;
    }

    /// <summary>信息图标里的「i」：上面一个点 + 下面一条圆头竖线。</summary>
    private static void DrawMarkInfo(Graphics graphics, RectangleF box, Color color)
    {
        var stroke = Math.Max(2f, box.Width * 0.10f);
        var centerX = box.Left + box.Width / 2f;
        var dot = Math.Max(2.5f, box.Width * 0.10f);

        using (var pen = new Pen(color, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            graphics.DrawLine(
                pen,
                centerX,
                box.Top + box.Height * 0.46f,
                centerX,
                box.Top + box.Height * 0.72f);
        }

        using var brush = new SolidBrush(color);
        graphics.FillEllipse(
            brush,
            new RectangleF(centerX - dot / 2f, box.Top + box.Height * 0.29f, dot, dot));
    }

    /// <summary>错误图标里的「×」：两条圆头斜线。</summary>
    private static void DrawMarkCross(Graphics graphics, RectangleF box, Color color)
    {
        var stroke = Math.Max(2f, box.Width * 0.11f);
        var centerX = box.Left + box.Width / 2f;
        var centerY = box.Top + box.Height / 2f;
        var arm = box.Width * 0.20f;

        using var pen = new Pen(color, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        graphics.DrawLine(pen, centerX - arm, centerY - arm, centerX + arm, centerY + arm);
        graphics.DrawLine(pen, centerX + arm, centerY - arm, centerX - arm, centerY + arm);
    }

    /// <summary>警告三角里的「!」：竖线 + 点，位置下移一点才落在三角形的重心上。</summary>
    private static void DrawMarkBang(Graphics graphics, RectangleF box, Color color)
    {
        var stroke = Math.Max(2f, box.Width * 0.095f);
        var centerX = box.Left + box.Width / 2f;
        var dot = Math.Max(2.5f, box.Width * 0.095f);

        using (var pen = new Pen(color, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            graphics.DrawLine(
                pen,
                centerX,
                box.Top + box.Height * 0.37f,
                centerX,
                box.Top + box.Height * 0.62f);
        }

        using var brush = new SolidBrush(color);
        graphics.FillEllipse(
            brush,
            new RectangleF(centerX - dot / 2f, box.Top + box.Height * 0.71f, dot, dot));
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DismissScrim();
            _fade.Dispose();
        }

        base.Dispose(disposing);

        if (disposing)
        {
            _titleFont?.Dispose();
            _bodyFont?.Dispose();
            _titleFont = null;
            _bodyFont = null;
        }
    }
}
