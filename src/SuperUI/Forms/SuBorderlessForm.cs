using System.ComponentModel;
using System.Runtime.InteropServices;
using SuperUI.Native;
using SuperUI.Theming;

namespace SuperUI.Forms;

/// <summary>
/// SuperUI 无边框窗口：自绘标题栏 + 圆角 + DWM 投影，支持拖动（含 Aero Snap：拖到屏幕顶部最大化、
/// 拖到左右两侧半屏）、双击标题栏最大化、八方向缩放、最大化贴合工作区（不遮挡任务栏），
/// 标题栏颜色随全局主题切换。
/// <para>
/// 窗体本身就是设计画布：控件可以直接拖到窗体上（也可以 <c>Controls.Add</c>），
/// 标题栏与四周缩放热区通过 <see cref="DisplayRectangle"/> 预留，不会和内容重叠。
/// </para>
/// </summary>
[Description("SuperUI 无边框窗口：自绘标题栏、拖动缩放、触顶最大化，标题栏颜色跟随全局主题。")]
public class SuBorderlessForm : Form, ISuThemed
{
    private readonly SuFontSync _fontSync;
    private readonly SuTitleBar _titleBar;
    private int _resizeBorderThickness = 6;
    private bool _resizable = true;
    private bool _titleBarVisible = true;
    private bool _roundedCorners = true;
    private bool _useDwmShadow = true;
    private int _titleBarHeight = -1;
    private bool _snapArmed;
    private bool _snapRemovedMaximizeBox;

    public SuBorderlessForm()
    {
        _fontSync = new SuFontSync(this);

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        DoubleBuffered = true;
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);

        BackColor = SuThemeManager.Current.Background;
        ClientSize = new Size(960, 640);

        _titleBar = new SuTitleBar
        {
            Dock = DockStyle.None,
            Title = Text,
        };

        Controls.Add(_titleBar);

        _titleBar.MinimizeRequested += (_, __) => WindowState = FormWindowState.Minimized;
        _titleBar.MaximizeRestoreRequested += (_, __) => ToggleMaximize();
        _titleBar.CloseRequested += (_, __) => Close();
        _titleBar.SizeChanged += TitleBarOnSizeChanged;

        SuThemeManager.Register(this);
        ApplyTheme();
        LayoutChrome();
    }

    /// <summary>标题栏（可继续追加自定义控件、改标题样式或 Logo）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SuTitleBar TitleBar => _titleBar;

    /// <summary>内容区可用矩形：客户区去掉标题栏与四周缩放热区，再扣除 <see cref="Control.Padding"/>。</summary>
    public override Rectangle DisplayRectangle
    {
        get
        {
            var rect = base.DisplayRectangle;
            var band = ScaledResizeBand;
            var top = _titleBarVisible ? _titleBar.Height : band;

            return new Rectangle(
                rect.X + band,
                rect.Y + top,
                Math.Max(0, rect.Width - band * 2),
                Math.Max(0, rect.Height - top - band));
        }
    }

    /// <summary>标题栏高度；-1 表示跟随主题的 <see cref="SuTheme.TitleBarHeight"/>。</summary>
    [Category("SuperUI")]
    [DefaultValue(-1)]
    [Description("标题栏高度（像素）。-1 表示跟随当前主题的 TitleBarHeight。")]
    public int TitleBarHeight
    {
        get => _titleBarHeight;
        set
        {
            var height = value < 0 ? -1 : Math.Max(12, value);
            if (_titleBarHeight == height)
            {
                return;
            }

            _titleBarHeight = height;
            ApplyTitleBarHeight();
            LayoutChrome();
            PerformLayout();
            Invalidate(true);
        }
    }

    /// <summary>是否显示最小化按钮。</summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    public bool ShowMinimizeBox
    {
        get => _titleBar.ShowMinimizeBox;
        set => _titleBar.ShowMinimizeBox = value;
    }

    /// <summary>是否显示最大化按钮。</summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    public bool ShowMaximizeBox
    {
        get => _titleBar.ShowMaximizeBox;
        set => _titleBar.ShowMaximizeBox = value;
    }

    /// <summary>是否显示关闭按钮。</summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    public bool ShowCloseBox
    {
        get => _titleBar.ShowCloseBox;
        set => _titleBar.ShowCloseBox = value;
    }

    /// <summary>是否显示标题栏（隐藏后整个客户区都是内容区，可拖动区域也随之消失）。</summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    public bool TitleBarVisible
    {
        get => _titleBarVisible;
        set
        {
            if (_titleBarVisible == value)
            {
                return;
            }

            _titleBarVisible = value;
            _titleBar.Visible = value;
            LayoutChrome();
            PerformLayout();
            Invalidate(true);
        }
    }

    /// <summary>窗口是否允许八方向缩放。</summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    public bool Resizable
    {
        get => _resizable;
        set
        {
            if (_resizable == value)
            {
                return;
            }

            _resizable = value;
            MaximumSize = value ? Size.Empty : Size;
            LayoutChrome();
            PerformLayout();
            Invalidate(true);
        }
    }

    /// <summary>缩放热区宽度（像素，会按 DPI 缩放）。</summary>
    [Category("SuperUI")]
    [DefaultValue(6)]
    public int ResizeBorderThickness
    {
        get => _resizeBorderThickness;
        set
        {
            var thickness = Math.Max(0, value);
            if (_resizeBorderThickness == thickness)
            {
                return;
            }

            _resizeBorderThickness = thickness;
            LayoutChrome();
            PerformLayout();
            Invalidate(true);
        }
    }

    /// <summary>是否请求系统圆角（Windows 11 生效）。</summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    public bool RoundedCorners
    {
        get => _roundedCorners;
        set
        {
            _roundedCorners = value;
            ApplyDwm();
        }
    }

    /// <summary>是否使用 DWM 投影（无边框窗口默认没有系统阴影）。</summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    public bool UseDwmShadow
    {
        get => _useDwmShadow;
        set
        {
            _useDwmShadow = value;
            ApplyDwm();
        }
    }

    /// <summary>当前主题。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SuTheme Theme => SuThemeManager.Current;

    /// <summary>最大化 / 还原切换。</summary>
    public void ToggleMaximize()
    {
        WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;
    }

    /// <summary>
    /// 拖动前给窗口临时挂上 <c>WS_THICKFRAME</c>（不触发边框重算，客户区与外观都不变），
    /// 让 Windows 的移动循环把这扇无边框窗口当成「可缩放窗口」，从而接管 Aero Snap：
    /// 拖到屏幕顶部最大化、拖到左右两侧半屏、拖到四角占四分之一屏。
    /// </summary>
    internal void ArmSnapStyle()
    {
        if (!IsHandleCreated || !_resizable || _snapArmed)
        {
            return;
        }

        var style = SuNative.GetWindowStyle(Handle);
        if ((style & SuNative.WS_THICKFRAME) != 0)
        {
            return;
        }

        // 没显示最大化按钮的窗口不该有「触顶最大化」，但左右半屏仍然保留。
        _snapRemovedMaximizeBox = !_titleBar.ShowMaximizeBox;
        var armed = style | SuNative.WS_THICKFRAME;
        if (_snapRemovedMaximizeBox)
        {
            armed &= ~SuNative.WS_MAXIMIZEBOX;
        }

        SuNative.SetWindowStyle(Handle, armed);
        _snapArmed = true;
    }

    /// <summary>
    /// 拖动结束后摘掉临时挂上的 <c>WS_THICKFRAME</c>。只清这一个样式位，其余保持现状——
    /// 吸附最大化时系统会自己往样式里写 <c>WS_MAXIMIZE</c>，用拖动前的快照整个盖回去会把最大化状态一起抹掉。
    /// </summary>
    internal void DisarmSnapStyle()
    {
        if (!_snapArmed || !IsHandleCreated)
        {
            return;
        }

        var style = SuNative.GetWindowStyle(Handle) & ~SuNative.WS_THICKFRAME;
        if (_snapRemovedMaximizeBox)
        {
            style |= SuNative.WS_MAXIMIZEBOX;
        }

        SuNative.SetWindowStyle(Handle, style);
        _snapArmed = false;
        _snapRemovedMaximizeBox = false;
    }

    /// <summary>
    /// 拖动收尾：系统是按「带边框窗口」算吸附矩形的，半屏 / 四角会比工作区多出一圈边框，
    /// 这里按光标落点把窗口重新贴到工作区上，无边框窗口吸附后严丝合缝。
    /// </summary>
    internal void FinishDragSnap()
    {
        if (!_resizable || !IsHandleCreated || WindowState != FormWindowState.Normal)
        {
            return;
        }

        var cursor = Cursor.Position;
        var screen = Screen.FromPoint(cursor);
        var workArea = screen.WorkingArea;
        var band = Math.Max(2, (int)Math.Round(6 * DeviceDpi / 96.0));

        var atLeft = cursor.X <= workArea.Left + band;
        var atRight = cursor.X >= workArea.Right - band;
        var atTop = cursor.Y <= workArea.Top + band;
        var atBottom = cursor.Y >= workArea.Bottom - band;

        var halfWidth = workArea.Width / 2;
        var halfHeight = workArea.Height / 2;

        Rectangle target;
        if (atTop && atLeft)
        {
            target = new Rectangle(workArea.Left, workArea.Top, halfWidth, halfHeight);
        }
        else if (atTop && atRight)
        {
            target = new Rectangle(workArea.Left + halfWidth, workArea.Top, workArea.Width - halfWidth, halfHeight);
        }
        else if (atBottom && atLeft)
        {
            target = new Rectangle(workArea.Left, workArea.Bottom - halfHeight, halfWidth, halfHeight);
        }
        else if (atBottom && atRight)
        {
            target = new Rectangle(workArea.Left + halfWidth, workArea.Bottom - halfHeight, workArea.Width - halfWidth, halfHeight);
        }
        else if (atLeft)
        {
            target = new Rectangle(workArea.Left, workArea.Top, halfWidth, workArea.Height);
        }
        else if (atRight)
        {
            target = new Rectangle(workArea.Left + halfWidth, workArea.Top, workArea.Width - halfWidth, workArea.Height);
        }
        else
        {
            // 触顶最大化由系统的最大化手势负责，这里不重复处理。
            return;
        }

        // 逐条边纠正：只有已经贴在目标位置上的那条边才会被挪正，
        // 用户自己摆放的位置不会被动。窗口最小尺寸比半屏还大时，系统那一侧会被夹住，这里也不会硬改。
        var tolerance = band * 4;

        bool Near(int current, int targetValue) => Math.Abs(current - targetValue) <= tolerance;

        var current = Bounds;
        var left = Near(current.Left, target.Left) ? target.Left : current.Left;
        var top = Near(current.Top, target.Top) ? target.Top : current.Top;
        var right = Near(current.Right, target.Right) ? target.Right : current.Right;
        var bottom = Near(current.Bottom, target.Bottom) ? target.Bottom : current.Bottom;

        var snapped = Rectangle.FromLTRB(left, top, right, bottom);
        if (snapped != current)
        {
            Bounds = snapped;
        }
    }

    /// <inheritdoc />
    public virtual void OnThemeChanged()
    {
        ApplyTheme();
        Invalidate(true);
    }

    private void ApplyTheme()
    {
        _fontSync.Apply(Theme);
        BackColor = Theme.Background;
        ApplyTitleBarHeight();
        LayoutChrome();
        PerformLayout();
    }

    private void ApplyTitleBarHeight()
    {
        var height = _titleBarHeight > 0 ? _titleBarHeight : Theme.TitleBarHeight;
        if (height > 0 && _titleBar.Height != height)
        {
            _titleBar.Height = height;
        }
    }

    /// <inheritdoc />
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        if (_titleBar is not null)
        {
            _titleBar.Title = Text;
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);

        if (_titleBar is not null)
        {
            _titleBar.IsMaximized = WindowState == FormWindowState.Maximized;
        }

        LayoutChrome();
    }

    private void TitleBarOnSizeChanged(object? sender, EventArgs e)
    {
        LayoutChrome();
        PerformLayout();
    }

    /// <summary>缩放热区宽度（已按 DPI 缩放）；不允许缩放时为 0。</summary>
    private int ScaledResizeBand
        => _resizable ? (int)Math.Round(_resizeBorderThickness * DeviceDpi / 96.0) : 0;

    /// <summary>把标题栏铺满客户区顶部；四周留白由 <see cref="DisplayRectangle"/> 负责。</summary>
    private void LayoutChrome()
    {
        if (_titleBar is null)
        {
            return;
        }

        _titleBar.TopResizeBand = Math.Max(4, ScaledResizeBand);
        _titleBar.SetBounds(0, 0, ClientSize.Width, _titleBar.Height);
        _titleBar.Visible = _titleBarVisible;
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (WindowState == FormWindowState.Maximized)
        {
            return;
        }

        using var pen = new Pen(Theme.BorderStrong);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyDwm();
    }

    private void ApplyDwm()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        try
        {
            var preference = _roundedCorners ? SuNative.DWMWCP_ROUND : SuNative.DWMWCP_DONOTROUND;
            SuNative.DwmSetWindowAttribute(
                Handle,
                SuNative.DWMWA_WINDOW_CORNER_PREFERENCE,
                ref preference,
                sizeof(int));

            if (_useDwmShadow)
            {
                // 1 像素玻璃边框，让 DWM 为无边框窗口补上投影。
                var margins = new SuNative.MARGINS
                {
                    cxLeftWidth = 1,
                    cxRightWidth = 1,
                    cyTopHeight = 1,
                    cyBottomHeight = 1,
                };
                SuNative.DwmExtendFrameIntoClientArea(Handle, ref margins);
            }
        }
        catch (DllNotFoundException)
        {
            // 老系统没有 dwmapi，忽略。
        }
        catch (EntryPointNotFoundException)
        {
            // 老系统缺导出，忽略。
        }
    }

    /// <inheritdoc />
    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case SuNative.WM_NCHITTEST:
                base.WndProc(ref m);
                if ((int)m.Result == SuNative.HTCLIENT)
                {
                    var screenPoint = SuNative.PointFromLParam(m.LParam);

                    if (TryResizeHit(screenPoint, out var hit))
                    {
                        m.Result = hit;
                        return;
                    }

                    if (IsDragArea(screenPoint))
                    {
                        m.Result = (IntPtr)SuNative.HTCAPTION;
                        return;
                    }
                }

                return;

            case SuNative.WM_GETMINMAXINFO:
                base.WndProc(ref m);
                FixMaximizedBounds(m.LParam);
                m.Result = IntPtr.Zero;
                return;

            default:
                base.WndProc(ref m);
                return;
        }
    }

    private bool IsDragArea(Point screenPoint)
    {
        if (!_titleBar.Visible)
        {
            return false;
        }

        var clientPoint = _titleBar.PointToClient(screenPoint);
        return _titleBar.IsDragArea(clientPoint);
    }

    private bool TryResizeHit(Point screenPoint, out IntPtr result)
    {
        result = IntPtr.Zero;

        if (!_resizable || WindowState == FormWindowState.Maximized)
        {
            return false;
        }

        var border = ScaledResizeBand;
        if (border <= 0)
        {
            return false;
        }

        var point = PointToClient(screenPoint);
        var left = point.X <= border;
        var right = point.X >= ClientSize.Width - border;
        var top = point.Y <= border;
        var bottom = point.Y >= ClientSize.Height - border;

        int code;
        if (left && top)
        {
            code = SuNative.HTTOPLEFT;
        }
        else if (right && top)
        {
            code = SuNative.HTTOPRIGHT;
        }
        else if (left && bottom)
        {
            code = SuNative.HTBOTTOMLEFT;
        }
        else if (right && bottom)
        {
            code = SuNative.HTBOTTOMRIGHT;
        }
        else if (left)
        {
            code = SuNative.HTLEFT;
        }
        else if (right)
        {
            code = SuNative.HTRIGHT;
        }
        else if (top)
        {
            code = SuNative.HTTOP;
        }
        else if (bottom)
        {
            code = SuNative.HTBOTTOM;
        }
        else
        {
            return false;
        }

        result = (IntPtr)code;
        return true;
    }

    private void FixMaximizedBounds(IntPtr infoPointer)
    {
        if (infoPointer == IntPtr.Zero)
        {
            return;
        }

        var info = Marshal.PtrToStructure<SuNative.MINMAXINFO>(infoPointer);
        var screen = Screen.FromHandle(Handle);
        var workArea = screen.WorkingArea;
        var bounds = screen.Bounds;

        info.ptMaxPosition.X = workArea.Left - bounds.Left;
        info.ptMaxPosition.Y = workArea.Top - bounds.Top;
        info.ptMaxSize.X = workArea.Width;
        info.ptMaxSize.Y = workArea.Height;

        if (!_resizable)
        {
            var width = (int)Math.Round(Width * DeviceDpi / 96.0);
            var height = (int)Math.Round(Height * DeviceDpi / 96.0);
            info.ptMinTrackSize.X = width;
            info.ptMinTrackSize.Y = height;
            info.ptMaxTrackSize.X = width;
            info.ptMaxTrackSize.Y = height;
        }

        Marshal.StructureToPtr(info, infoPointer, false);
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
