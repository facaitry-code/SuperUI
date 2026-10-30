using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using SuperUI.Rendering;
using SuperUI.Theming;

namespace SuperUI.Controls;

/// <summary>
/// SuperUI 主题输入框：圆角边框、聚焦高亮（带缓动）、占位符、可选清除按钮、多行/密码/只读支持，
/// 颜色与字体全部跟随全局主题。
/// </summary>
[Description("SuperUI 主题输入框：圆角、聚焦高亮、占位符、清除按钮，颜色随全局主题实时切换。")]
[DefaultEvent(nameof(TextChanged))]
[DefaultProperty(nameof(Text))]
public class SuTextBox : SuControlBase
{
    private readonly SuTween _focusTween;
    private readonly TextBox _inner;
    private readonly Label _placeholder;
    private readonly Label _clear;
    private string _placeholderText = string.Empty;
    private int _radius = -1;
    private bool _autoHeight = true;
    private bool _showClearButton;
    private bool _hovered;

    public SuTextBox()
    {
        SetStyle(ControlStyles.Selectable, true);
        Padding = new Padding(12, 8, 12, 8);
        Cursor = Cursors.IBeam;

        _focusTween = new SuTween();
        _focusTween.Changed += (_, __) => Invalidate();

        _inner = new TextBox
        {
            BorderStyle = BorderStyle.None,
            TabStop = false,
        };

        _placeholder = new Label
        {
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false,
            Cursor = Cursors.IBeam,
            Visible = false,
        };

        _clear = new Label
        {
            AutoSize = false,
            Text = "\u2715",
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            Visible = false,
        };

        Controls.Add(_inner);
        Controls.Add(_placeholder);
        Controls.Add(_clear);
        _placeholder.BringToFront();
        _clear.BringToFront();

        _inner.TextChanged += InnerOnTextChanged;
        _inner.GotFocus += InnerOnGotFocus;
        _inner.LostFocus += InnerOnLostFocus;
        _inner.MouseEnter += (_, __) => SetHovered(true);
        _inner.MouseLeave += (_, __) => RefreshHoverFromCursor();
        _placeholder.Click += (_, __) => _inner.Focus();
        _clear.Click += ClearButtonOnClick;

        ApplyTheme();
        Height = PreferredHeight;
    }

    /// <summary>内部原生 TextBox（需要在极少数场景下直接访问时使用）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextBox InnerTextBox => _inner;

    /// <summary>输入内容。</summary>
    [Category("SuperUI")]
    [DefaultValue("")]
    [AllowNull]
    public override string Text
    {
        get => _inner.Text;
        set
        {
            var text = value ?? string.Empty;
            if (_inner.Text == text)
            {
                return;
            }

            _inner.Text = text;
        }
    }

    /// <summary>占位符文字（内容为空且未聚焦时显示）。</summary>
    [Category("SuperUI")]
    [DefaultValue("")]
    [Description("占位提示文字，输入框为空且未获得焦点时显示。")]
    public string PlaceholderText
    {
        get => _placeholderText;
        set
        {
            value ??= string.Empty;
            if (_placeholderText == value)
            {
                return;
            }

            _placeholderText = value;
            _placeholder.Text = value;
            UpdatePlaceholderVisibility();
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

    /// <summary>是否根据内容/字体自动计算高度。</summary>
    [Category("SuperUI")]
    [DefaultValue(true)]
    [Description("为 true 时控件高度由字体与内边距决定；需要固定高度时设为 false。")]
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

    /// <summary>是否显示内容清除按钮。</summary>
    [Category("SuperUI")]
    [DefaultValue(false)]
    public bool ShowClearButton
    {
        get => _showClearButton;
        set
        {
            if (_showClearButton == value)
            {
                return;
            }

            _showClearButton = value;
            UpdateClearVisibility();
            LayoutInner();
            Invalidate();
        }
    }

    /// <summary>是否多行输入。</summary>
    [Category("SuperUI")]
    [DefaultValue(false)]
    public bool Multiline
    {
        get => _inner.Multiline;
        set
        {
            if (_inner.Multiline == value)
            {
                return;
            }

            _inner.Multiline = value;
            _inner.AcceptsReturn = value;
            _inner.ScrollBars = ScrollBars.None;
            _inner.WordWrap = true;
            LayoutInner();
            if (_autoHeight)
            {
                Height = PreferredHeight;
            }

            Invalidate();
        }
    }

    /// <summary>是否只读。</summary>
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
            ApplyColors();
            Invalidate();
        }
    }

    /// <summary>密码掩码字符。</summary>
    [Category("SuperUI")]
    [DefaultValue('\0')]
    public char PasswordChar
    {
        get => _inner.PasswordChar;
        set => _inner.PasswordChar = value;
    }

    /// <summary>使用系统密码掩码。</summary>
    [Category("SuperUI")]
    [DefaultValue(false)]
    public bool UseSystemPasswordChar
    {
        get => _inner.UseSystemPasswordChar;
        set => _inner.UseSystemPasswordChar = value;
    }

    /// <summary>最大字符数。</summary>
    [Category("SuperUI")]
    [DefaultValue(32767)]
    public int MaxLength
    {
        get => _inner.MaxLength;
        set => _inner.MaxLength = value;
    }

    /// <summary>建议高度（由字体与内边距推导）。</summary>
    [Browsable(false)]
    public int PreferredHeight
    {
        get
        {
            if (_inner is null)
            {
                return 38;
            }

            if (_inner.Multiline)
            {
                return Padding.Vertical + 78;
            }

            return _inner.PreferredHeight + Padding.Vertical + 2;
        }
    }

    /// <summary>选中全部文本。</summary>
    public void SelectAll() => _inner.SelectAll();

    /// <summary>清空内容。</summary>
    public void Clear() => Text = string.Empty;

    /// <summary>让内部输入区获得焦点。</summary>
    public new bool Focus() => _inner.Focus();

    /// <summary>选区起点。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectionStart
    {
        get => _inner.SelectionStart;
        set => _inner.SelectionStart = value;
    }

    /// <summary>选区长度。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectionLength
    {
        get => _inner.SelectionLength;
        set => _inner.SelectionLength = value;
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
        _placeholder.BackColor = surface;
        _placeholder.ForeColor = theme.TextPlaceholder;
        _clear.BackColor = surface;
        _clear.ForeColor = theme.TextSecondary;
        _placeholder.Text = _placeholderText;
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        SuDraw.SetupQuality(graphics);

        var theme = Theme;
        var focus = _focusTween.Value;
        var isFocused = _inner.Focused;

        var surface = ReadOnly || !Enabled ? theme.SurfaceAlt : theme.Surface;
        if (!Enabled)
        {
            surface = SuDraw.Blend(surface, theme.Background, 0.35f);
        }

        var borderColor = theme.Border;
        if (_hovered && !isFocused)
        {
            borderColor = theme.BorderStrong;
        }

        borderColor = SuDraw.Blend(borderColor, theme.Primary, focus * 0.9f);

        var radius = _radius >= 0 ? _radius : theme.Radius;
        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);

        // 圆角外区域显式铺父容器底色，避免缓冲池复用残留上一帧像素。
        using (var clearBrush = new SolidBrush(BackColor))
        {
            graphics.FillRectangle(clearBrush, ClientRectangle);
        }

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
    }

    /// <summary>布局内部原生控件（输入区、占位符、清除按钮）。</summary>
    private void LayoutInner()
    {
        if (_inner is null || _placeholder is null || _clear is null)
        {
            return;
        }

        var padLeft = Padding.Left;
        var padTop = Padding.Top;
        var reserve = _showClearButton && _clear.Visible ? 22 : 0;
        var innerWidth = Math.Max(12, ClientSize.Width - Padding.Horizontal - reserve);

        if (_inner.Multiline)
        {
            var height = Math.Max(24, ClientSize.Height - Padding.Vertical);
            _inner.Bounds = new Rectangle(padLeft, padTop, innerWidth, height);
            _placeholder.Bounds = new Rectangle(padLeft + 1, padTop + 2, innerWidth, Font.Height + 2);
        }
        else
        {
            var innerHeight = _inner.PreferredHeight;
            var top = Math.Max(padTop, (ClientSize.Height - innerHeight) / 2);
            _inner.Bounds = new Rectangle(padLeft, top, innerWidth, innerHeight);
            _placeholder.Bounds = new Rectangle(padLeft, top, innerWidth, innerHeight);
        }

        var glyphSize = Math.Max(14, Font.Height);
        _clear.Bounds = new Rectangle(
            Math.Max(0, ClientSize.Width - Padding.Right - glyphSize),
            Math.Max(0, (ClientSize.Height - glyphSize) / 2),
            glyphSize,
            glyphSize);
    }

    private void InnerOnTextChanged(object? sender, EventArgs e)
    {
        UpdatePlaceholderVisibility();
        UpdateClearVisibility();
        LayoutInner();
        OnTextChanged(e);
    }

    private void InnerOnGotFocus(object? sender, EventArgs e)
    {
        _focusTween.To(1f, 120);
        UpdatePlaceholderVisibility();
        Invalidate();
    }

    private void InnerOnLostFocus(object? sender, EventArgs e)
    {
        _focusTween.To(0f, 160);
        UpdatePlaceholderVisibility();
        Invalidate();
    }

    private void UpdatePlaceholderVisibility()
    {
        if (_placeholder is null)
        {
            return;
        }

        _placeholder.Text = _placeholderText;
        _placeholder.Visible = _placeholderText.Length > 0
                               && _inner.Text.Length == 0
                               && !_inner.Focused;
    }

    private void UpdateClearVisibility()
    {
        if (_clear is null)
        {
            return;
        }

        _clear.Visible = _showClearButton && _inner.Text.Length > 0;
    }

    private void ClearButtonOnClick(object? sender, EventArgs e)
    {
        Text = string.Empty;
        _inner.Focus();
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
    protected override void OnMouseEnter(EventArgs e)
    {
        SetHovered(true);
        base.OnMouseEnter(e);
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        RefreshHoverFromCursor();
        base.OnMouseLeave(e);
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!_inner.Focused)
        {
            _inner.Focus();
        }
    }

    /// <inheritdoc />
    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        _inner.Focus();
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
        if (_autoHeight)
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
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _focusTween.Dispose();
        }

        base.Dispose(disposing);
    }
}
