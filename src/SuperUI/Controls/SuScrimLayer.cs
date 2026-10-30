using System.Drawing;
using SuperUI.Rendering;

namespace SuperUI.Controls;

/// <summary>
/// 弹窗遮罩层：铺满宿主控件的客户区，绘制时先铺一张宿主当前画面的快照、再叠一层半透明色。
/// <para>
/// 之所以要先拍快照：WinForms 的子控件之间不会互相合成，直接给遮罩刷半透明底色只会得到一块纯色板，
/// 被盖住的表格 / 按钮 / 菜单全都看不见；「先画快照、再叠半透明」才能得到"整窗变暗"的效果。
/// </para>
/// 由 <see cref="SuperUI.Forms.SuMessageBoxDialog"/> 在需要遮罩时创建，不单独放进工具箱。
/// </summary>
internal sealed class SuScrimLayer : Control
{
    private readonly SuTween _fade;
    private Bitmap? _backdrop;
    private Control? _host;
    private Color _scrimColor = Color.Black;
    private float _opacity = 0.45f;

    public SuScrimLayer()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);

        TabStop = false;
        Cursor = Cursors.Default;

        _fade = new SuTween();
        _fade.Changed += (_, __) => Invalidate();

        // 放到最后：Visible 变化会触发 OnVisibleChanged，此时字段必须已经就绪。
        Visible = false;
    }

    /// <summary>遮罩颜色（默认黑）。</summary>
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
            Invalidate();
        }
    }

    /// <summary>不透明度 0..1。</summary>
    public float Opacity
    {
        get => _opacity;
        set
        {
            var opacity = SuMath.Clamp(value, 0f, 1f);
            if (Math.Abs(_opacity - opacity) < 0.001f)
            {
                return;
            }

            _opacity = opacity;
            Invalidate();
        }
    }

    /// <summary>
    /// 把遮罩挂到宿主控件上并淡入：铺满整个客户区（不是 <see cref="Control.DisplayRectangle"/>，
    /// 这样无边框窗口的标题栏也会一起压暗）、置于最上层。宿主不可用时返回 null。
    /// </summary>
    public static SuScrimLayer? Show(Control? host, Color color, float opacity)
    {
        if (host is null
            || host.IsDisposed
            || !host.IsHandleCreated
            || host.ClientSize.Width <= 0
            || host.ClientSize.Height <= 0)
        {
            return null;
        }

        var layer = new SuScrimLayer
        {
            _scrimColor = color,
            _opacity = SuMath.Clamp(opacity, 0f, 1f),
        };

        layer.Attach(host);

        // 顺序很关键：先入树 → 再拍快照（此时自己还不可见，不会被拍进去）→ 最后显示。
        layer.CaptureBackdrop();
        layer.Visible = true;
        layer.BringToFront();
        layer._fade.Snap(0f);
        layer._fade.To(1f, 140);
        return layer;
    }

    /// <summary>撤掉遮罩（从宿主上摘下来并隐身）；可重复调用。</summary>
    public void Dismiss()
    {
        if (_host is not null)
        {
            _host.SizeChanged -= HostOnSizeChanged;

            if (!_host.IsDisposed)
            {
                _host.Controls.Remove(this);
            }

            _host = null;
        }

        _fade.Snap(0f);
        Visible = false;
    }

    private void Attach(Control host)
    {
        _host = host;
        BackColor = host.BackColor;

        // 不用 Dock = Fill：SuBorderlessForm 的 DisplayRectangle 会把标题栏让出去，
        // 遮罩要盖住整窗（模态期间宿主本来就不接受操作），所以手动铺满客户区。
        Dock = DockStyle.None;
        Bounds = new Rectangle(Point.Empty, host.ClientSize);

        host.Controls.Add(this);
        host.SizeChanged += HostOnSizeChanged;
    }

    private void HostOnSizeChanged(object? sender, EventArgs e)
    {
        if (_host is null || _host.IsDisposed)
        {
            return;
        }

        Bounds = new Rectangle(Point.Empty, _host.ClientSize);
        Invalidate();
    }

    /// <summary>把宿主当前画面截成位图（绘制时按控件尺寸拉伸，宿主变了大小也不用重拍）。</summary>
    private void CaptureBackdrop()
    {
        DisposeBackdrop();

        if (_host is null)
        {
            return;
        }

        try
        {
            var size = _host.ClientSize;
            var bitmap = new Bitmap(Math.Max(1, size.Width), Math.Max(1, size.Height));
            _host.DrawToBitmap(bitmap, new Rectangle(Point.Empty, size));
            _backdrop = bitmap;
        }
        catch (Exception)
        {
            // 少数控件（绘制中 / 无句柄）无法快照，退化成纯色遮罩。
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

        using (var clear = new SolidBrush(BackColor))
        {
            graphics.FillRectangle(clear, ClientRectangle);
        }

        // 1) 宿主快照：模态期间宿主本来就不响应操作，静态画面足够。
        if (_backdrop is not null)
        {
            graphics.DrawImage(_backdrop, new Rectangle(Point.Empty, ClientRectangle.Size));
        }

        // 2) 半透明色：这一层才是"变暗"，两帧合成才有透出来的内容。
        var alpha = (int)Math.Round(255 * _opacity * _fade.Value);
        if (alpha > 0)
        {
            using var brush = new SolidBrush(Color.FromArgb(SuMath.Clamp(alpha, 0, 255), _scrimColor));
            graphics.FillRectangle(brush, ClientRectangle);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Dismiss();
            DisposeBackdrop();
            _fade.Dispose();
        }

        base.Dispose(disposing);
    }
}
