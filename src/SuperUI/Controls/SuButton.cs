using System.ComponentModel;
using SuperUI.Rendering;
using SuperUI.Theming;

namespace SuperUI.Controls;

/// <summary>按钮外观风格。</summary>
public enum SuButtonVariant
{
    /// <summary>实心主色按钮。</summary>
    Primary,

    /// <summary>描边按钮（白底/表面底 + 边框）。</summary>
    Secondary,

    /// <summary>无底色按钮，悬停时才出现淡背景。</summary>
    Ghost,

    /// <summary>危险操作按钮（红）。</summary>
    Danger,

    /// <summary>文字链接按钮。</summary>
    Link,
}

/// <summary>
/// SuperUI 主题按钮：完全自绘（圆角、悬停缓动、聚焦光环），颜色全部来自 <see cref="SuTheme"/>，
/// 主题切换时自动刷新，无需逐个改属性。
/// </summary>
[Description("SuperUI 主题按钮：Primary / Secondary / Ghost / Danger / Link 五种风格，颜色随全局主题实时切换。")]
[DefaultEvent(nameof(Click))]
public class SuButton : Button, ISuThemed
{
    private readonly SuFontSync _fontSync;
    private readonly SuTween _hoverTween;
    private readonly SuSpinnerAnimator _spinner;
    private SuButtonVariant _variant = SuButtonVariant.Primary;
    private int _radius = -1;
    private Color? _backdropColor;
    private Image? _icon;
    private string? _iconGlyph;
    private string _iconFontName = "Segoe MDL2 Assets";
    private int _iconSize = 16;
    private int _iconGap = 6;
    private bool _isLoading;
    private string? _loadingText;
    private Font? _iconFont;
    private bool _hovered;
    private bool _pressed;
    private bool _initializing = true;

    public SuButton()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor,
            true);

        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        BackColor = SuThemeManager.Current.Background;
        Cursor = Cursors.Hand;
        Size = new Size(116, 36);

        _fontSync = new SuFontSync(this);
        _hoverTween = new SuTween();
        _hoverTween.Changed += (_, __) => Invalidate();

        _spinner = new SuSpinnerAnimator();
        _spinner.Tick += (_, __) => Invalidate();

        SuThemeManager.Register(this);
        ApplyTheme();
        _initializing = false;
    }

    /// <summary>左侧图标（优先级高于 <see cref="IconGlyph"/>，为空时回退到按钮自带的 Image）。</summary>
    [Category("SuperUI")]
    [DefaultValue(null)]
    [Description("按钮左侧图标图片。")]
    public Image? Icon
    {
        get => _icon;
        set
        {
            if (ReferenceEquals(_icon, value))
            {
                return;
            }

            _icon = value;
            Invalidate();
        }
    }

    /// <summary>图标字体里的一段字形（例如 Segoe MDL2 Assets 的 "\uE72C"）。</summary>
    [Category("SuperUI")]
    [DefaultValue(null)]
    [Description("图标字体字形，配合 IconFont 使用（Icon 为空时生效）。")]
    public string? IconGlyph
    {
        get => _iconGlyph;
        set
        {
            if (_iconGlyph == value)
            {
                return;
            }

            _iconGlyph = value;

            // 先备好图标字体，否则会“占位但画不出图”
            if (!string.IsNullOrEmpty(_iconGlyph) && _iconFont is null)
            {
                RebuildIconFont();
            }

            Invalidate();
        }
    }

    /// <summary>字形图标所用字体。</summary>
    [Category("SuperUI")]
    [DefaultValue("Segoe MDL2 Assets")]
    public string IconFont
    {
        get => _iconFontName;
        set
        {
            value = string.IsNullOrWhiteSpace(value) ? "Segoe MDL2 Assets" : value;
            if (_iconFontName == value)
            {
                return;
            }

            _iconFontName = value;
            RebuildIconFont();
            Invalidate();
        }
    }

    /// <summary>图标（或加载指示器）的边长。</summary>
    [Category("SuperUI")]
    [DefaultValue(16)]
    public int IconSize
    {
        get => _iconSize;
        set
        {
            var size = Math.Max(8, value);
            if (_iconSize == size)
            {
                return;
            }

            _iconSize = size;
            RebuildIconFont();
            Invalidate();
        }
    }

    /// <summary>图标与文字的间距。</summary>
    [Category("SuperUI")]
    [DefaultValue(6)]
    public int IconGap
    {
        get => _iconGap;
        set
        {
            var gap = Math.Max(0, value);
            if (_iconGap == gap)
            {
                return;
            }

            _iconGap = gap;
            Invalidate();
        }
    }

    /// <summary>加载中：显示旋转指示器、屏蔽点击（外观保持正常态，不置灰）。</summary>
    [Category("SuperUI")]
    [DefaultValue(false)]
    [Description("加载中状态：显示旋转指示器并屏蔽点击。")]
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading == value)
            {
                return;
            }

            _isLoading = value;
            _pressed = false;

            if (value)
            {
                _spinner.Start();
            }
            else
            {
                _spinner.Stop();
            }

            Cursor = value || !Enabled ? Cursors.Default : Cursors.Hand;
            Invalidate();
        }
    }

    /// <summary>加载中显示的文字；为空时沿用 <see cref="Control.Text"/>。</summary>
    [Category("SuperUI")]
    [DefaultValue(null)]
    public string? LoadingText
    {
        get => _loadingText;
        set
        {
            if (_loadingText == value)
            {
                return;
            }

            _loadingText = value;
            if (_isLoading)
            {
                Invalidate();
            }
        }
    }

    /// <summary>按钮风格，默认 <see cref="SuButtonVariant.Primary"/>。</summary>
    [Category("SuperUI")]
    [DefaultValue(SuButtonVariant.Primary)]
    [Description("按钮风格：Primary / Secondary / Ghost / Danger / Link。")]
    public SuButtonVariant Variant
    {
        get => _variant;
        set
        {
            if (_variant == value)
            {
                return;
            }

            _variant = value;
            Invalidate();
        }
    }

    /// <summary>圆角半径，-1 表示跟随主题。</summary>
    [Category("SuperUI")]
    [DefaultValue(-1)]
    [Description("圆角半径，-1 表示跟随主题 Radius。")]
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

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SuTheme Theme => SuThemeManager.Current;

    /// <summary>
    /// Ghost / Link 风格按钮的底色（默认自动取父容器底色，放在卡片上时无需手动设置）。
    /// </summary>
    [Category("SuperUI")]
    [Description("Ghost / Link 风格所用底色，null 表示自动取父容器底色。")]
    public Color? BackdropColor
    {
        get => _backdropColor;
        set
        {
            if (_backdropColor == value)
            {
                return;
            }

            _backdropColor = value;
            ApplyBackdrop();
            Invalidate();
        }
    }

    /// <summary>实际使用的底色。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Backdrop => SuBackdrop.Resolve(this, _backdropColor);

    /// <inheritdoc />
    public virtual void OnThemeChanged()
    {
        ApplyTheme();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs pevent)
    {
        var graphics = pevent.Graphics;
        SuDraw.SetupQuality(graphics);

        var theme = Theme;
        var colors = ResolveColors(theme);
        var radius = _radius >= 0 ? _radius : theme.Radius;
        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);

        // WinForms 的缓冲池在复用 Graphics 缓冲时不会清理上一帧像素，
        // 圆角外的 1 像素区域必须显式铺底色，否则会残留其它控件的内容。
        using (var clearBrush = new SolidBrush(BackColor))
        {
            graphics.FillRectangle(clearBrush, ClientRectangle);
        }

        using (var path = SuDraw.RoundedRect(rect, radius))
        {
            if (colors.Back.A > 0)
            {
                using var brush = new SolidBrush(colors.Back);
                graphics.FillPath(brush, path);
            }

            if (colors.Border.A > 0)
            {
                using var pen = new Pen(colors.Border, 1f);
                graphics.DrawPath(pen, path);
            }
        }

        if (Focused && ShowFocusCues && TabStop && Enabled && colors.Fore.A > 0)
        {
            var inner = RectangleF.Inflate(rect, -2.5f, -2.5f);
            using var innerPath = SuDraw.RoundedRect(inner, Math.Max(0, radius - 2));
            using var focusPen = new Pen(SuDraw.Alpha(colors.Fore, 130), 1f);
            graphics.DrawPath(focusPen, innerPath);
        }

        DrawContent(graphics, colors.Fore);
    }

    /// <inheritdoc />
    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        // Ghost / Link 需要透出父容器底色：交给默认实现按“透明背景”合成父容器像素。
        base.OnPaintBackground(pevent);
    }

    private void DrawContent(Graphics graphics, Color foreColor)
    {
        var bounds = new Rectangle(
            Padding.Left + 6,
            Padding.Top,
            Math.Max(0, Width - Padding.Horizontal - 12),
            Math.Max(0, Height - Padding.Vertical));

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var flags = TextFormatFlags.NoPadding
                    | TextFormatFlags.SingleLine
                    | TextFormatFlags.EndEllipsis
                    | TextFormatFlags.VerticalCenter;

        if (!UseMnemonic)
        {
            flags |= TextFormatFlags.NoPrefix;
        }

        var horizontal = TextAlign switch
        {
            ContentAlignment.TopLeft or ContentAlignment.MiddleLeft or ContentAlignment.BottomLeft => TextFormatFlags.Left,
            ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight => TextFormatFlags.Right,
            _ => TextFormatFlags.HorizontalCenter,
        };

        var text = _isLoading && !string.IsNullOrEmpty(_loadingText) ? _loadingText : Text;

        // 左侧内容：加载指示器 > 图标图片 > 字形图标 > 原生 Image
        var iconKind = ButtonIconKind.None;
        var iconWidth = 0;

        if (_isLoading)
        {
            iconKind = ButtonIconKind.Spinner;
            iconWidth = _iconSize;
        }
        else if (_icon is not null)
        {
            iconKind = ButtonIconKind.Image;
            iconWidth = _iconSize;
        }
        else if (HasDrawableGlyph())
        {
            iconKind = ButtonIconKind.Glyph;
            iconWidth = _iconSize;
        }
        else if (Image is not null)
        {
            iconKind = ButtonIconKind.Image;
            iconWidth = Image.Width;
        }

        // 没有图标：直接按 TextAlign 在整块里对齐，交给 DrawText 做像素级居中
        if (iconKind == ButtonIconKind.None)
        {
            if (!string.IsNullOrEmpty(text))
            {
                TextRenderer.DrawText(graphics, text, Font, bounds, foreColor, flags | horizontal);
            }

            return;
        }

        // 有图标：图标 + 文字作为一组整体对齐
        var textSize = string.IsNullOrEmpty(text)
            ? Size.Empty
            : TextRenderer.MeasureText(graphics, text, Font, new Size(int.MaxValue, int.MaxValue), flags);

        var gap = textSize.Width > 0 ? _iconGap : 0;
        var totalWidth = iconWidth + gap + textSize.Width;
        var left = horizontal switch
        {
            TextFormatFlags.Left => bounds.Left + 2,
            TextFormatFlags.Right => bounds.Right - totalWidth - 2,
            _ => bounds.Left + (bounds.Width - totalWidth) / 2,
        };

        var centerY = bounds.Top + bounds.Height / 2f;

        if (iconKind != ButtonIconKind.None)
        {
            var iconRect = new RectangleF(left, centerY - _iconSize / 2f, _iconSize, _iconSize);

            switch (iconKind)
            {
                case ButtonIconKind.Spinner:
                    SuSpinner.Draw(
                        graphics,
                        iconRect,
                        _spinner.Angle,
                        foreColor,
                        Math.Max(1.8f, _iconSize / 9f),
                        SuDraw.Alpha(foreColor, 70));
                    break;

                case ButtonIconKind.Glyph when _iconFont is not null:
                    TextRenderer.DrawText(
                        graphics,
                        _iconGlyph,
                        _iconFont,
                        Rectangle.Round(iconRect),
                        foreColor,
                        TextFormatFlags.HorizontalCenter
                        | TextFormatFlags.VerticalCenter
                        | TextFormatFlags.NoPadding
                        | TextFormatFlags.NoPrefix);
                    break;

                case ButtonIconKind.Image:
                    var image = _icon ?? Image;
                    if (image is not null)
                    {
                        DrawIconImage(graphics, image, iconRect);
                    }

                    break;
            }
        }

        if (textSize.Width > 0)
        {
            var textLeft = left + iconWidth + gap;
            var textBounds = new Rectangle(
                textLeft,
                bounds.Top,
                Math.Max(0, bounds.Right - textLeft),
                bounds.Height);

            // 注意：这里只能用 Left，不能把右上方的 HorizontalCenter 一起 OR 上，
            // 否则 DT_CENTER 会覆盖 DT_LEFT，文字会偏到右半边。
            TextRenderer.DrawText(
                graphics,
                text,
                Font,
                textBounds,
                foreColor,
                flags | TextFormatFlags.Left);
        }
    }

    /// <summary>按原比例缩放居中绘制图标图片（不拉伸变形）。</summary>
    private static void DrawIconImage(Graphics graphics, Image image, RectangleF slot)
    {
        var scale = Math.Min(slot.Width / image.Width, slot.Height / image.Height);
        var width = Math.Max(1f, image.Width * scale);
        var height = Math.Max(1f, image.Height * scale);

        graphics.DrawImage(
            image,
            new RectangleF(
                slot.Left + (slot.Width - width) / 2f,
                slot.Top + (slot.Height - height) / 2f,
                width,
                height));
    }

    /// <summary>字形字体可用且该字形有实际宽度时才占位（避免“留白却画不出图”）。</summary>
    private bool HasDrawableGlyph()
    {
        if (string.IsNullOrEmpty(_iconGlyph) || _iconFont is null)
        {
            return false;
        }

        var size = TextRenderer.MeasureText(
            _iconGlyph,
            _iconFont,
            new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

        return size.Width > 0;
    }

    private enum ButtonIconKind
    {
        None,
        Spinner,
        Glyph,
        Image,
    }

    private void RebuildIconFont()
    {
        var previous = _iconFont;
        Font? font = null;

        try
        {
            font = new Font(_iconFontName, Math.Max(6f, _iconSize * 0.75f), FontStyle.Regular, GraphicsUnit.Point);
        }
        catch (ArgumentException)
        {
            font = null;
        }

        _iconFont = font;

        if (previous is not null && !ReferenceEquals(previous, font))
        {
            previous.Dispose();
        }
    }

    private SuButtonColors ResolveColors(SuTheme theme)
    {
        var backdrop = Backdrop;
        var state = !Enabled
            ? SuButtonState.Disabled
            : _pressed && _hovered
                ? SuButtonState.Pressed
                : _hovered
                    ? SuButtonState.Hover
                    : SuButtonState.Normal;

        var normal = Lookup(theme, SuButtonState.Normal, backdrop);
        if (state != SuButtonState.Hover && state != SuButtonState.Pressed)
        {
            return normal;
        }

        var target = Lookup(theme, state, backdrop);
        if (!_hoverTween.IsAnimating && _hoverTween.Value >= 0.999f)
        {
            return target;
        }

        var t = _pressed ? 1f : _hoverTween.Value;
        return new SuButtonColors(
            SuDraw.Blend(normal.Back, target.Back, t),
            SuDraw.Blend(normal.Fore, target.Fore, t),
            SuDraw.Blend(normal.Border, target.Border, t));
    }

    private SuButtonColors Lookup(SuTheme t, SuButtonState state, Color backdrop) => Variant switch
    {
        SuButtonVariant.Primary => state switch
        {
            SuButtonState.Hover => new(t.PrimaryHover, t.TextOnAccent, t.PrimaryHover),
            SuButtonState.Pressed => new(t.PrimaryPressed, t.TextOnAccent, t.PrimaryPressed),
            SuButtonState.Disabled => new(t.PrimaryDisabled, SuDraw.Blend(t.TextOnAccent, t.PrimaryDisabled, 0.45f), t.PrimaryDisabled),
            _ => new(t.Primary, t.TextOnAccent, t.Primary),
        },
        SuButtonVariant.Secondary => state switch
        {
            SuButtonState.Hover => new(t.SurfaceAlt, t.TextPrimary, t.BorderStrong),
            SuButtonState.Pressed => new(SuDraw.Blend(t.SurfaceAlt, t.Border, 0.45f), t.TextPrimary, t.Primary),
            SuButtonState.Disabled => new(t.Surface, t.TextDisabled, t.Border),
            _ => new(t.Surface, t.TextPrimary, t.Border),
        },
        SuButtonVariant.Ghost => state switch
        {
            SuButtonState.Hover => new(t.SurfaceAlt, t.TextPrimary, Color.Transparent),
            SuButtonState.Pressed => new(SuDraw.Blend(t.SurfaceAlt, t.Border, 0.5f), t.TextPrimary, Color.Transparent),
            SuButtonState.Disabled => new(backdrop, t.TextDisabled, Color.Transparent),
            _ => new(backdrop, t.TextPrimary, Color.Transparent),
        },
        SuButtonVariant.Danger => state switch
        {
            SuButtonState.Hover => new(t.DangerHover, t.TextOnAccent, t.DangerHover),
            SuButtonState.Pressed => new(t.DangerPressed, t.TextOnAccent, t.DangerPressed),
            SuButtonState.Disabled => new(SuDraw.Blend(t.Danger, t.Background, 0.55f), SuDraw.Blend(t.TextOnAccent, t.Background, 0.35f), Color.Transparent),
            _ => new(t.Danger, t.TextOnAccent, t.Danger),
        },
        SuButtonVariant.Link => state switch
        {
            SuButtonState.Hover => new(backdrop, t.PrimaryHover, Color.Transparent),
            SuButtonState.Pressed => new(backdrop, t.PrimaryPressed, Color.Transparent),
            SuButtonState.Disabled => new(backdrop, t.TextDisabled, Color.Transparent),
            _ => new(backdrop, t.Primary, Color.Transparent),
        },
        _ => new(t.Primary, t.TextOnAccent, t.Primary),
    };

    private void ApplyTheme()
    {
        _fontSync.Apply(Theme);
        ApplyBackdrop();
        ForeColor = Lookup(Theme, SuButtonState.Normal, Backdrop).Fore;

        if (_iconFont is null && !string.IsNullOrEmpty(_iconGlyph))
        {
            RebuildIconFont();
        }
    }

    private void ApplyBackdrop()
    {
        var color = Backdrop;
        if (BackColor != color)
        {
            BackColor = color;
        }
    }

    /// <inheritdoc />
    protected override void OnParentBackColorChanged(EventArgs e)
    {
        base.OnParentBackColorChanged(e);
        ApplyBackdrop();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        ApplyBackdrop();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        _hoverTween.To(1f);
        base.OnMouseEnter(e);
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _pressed = false;
        _hoverTween.To(0f);
        base.OnMouseLeave(e);
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        if (mevent.Button == MouseButtons.Left && !_isLoading)
        {
            _pressed = true;
            Invalidate();
        }

        base.OnMouseDown(mevent);
    }

    /// <inheritdoc />
    protected override void OnClick(EventArgs e)
    {
        // 加载中不触发点击（DialogResult、AcceptButton 等都会被一起挡住）
        if (_isLoading)
        {
            return;
        }

        base.OnClick(e);
    }

    /// <inheritdoc />
    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        if (mevent.Button == MouseButtons.Left)
        {
            _pressed = false;
            Invalidate();
        }

        base.OnMouseUp(mevent);
    }

    /// <inheritdoc />
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
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
        if (_initializing)
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
            _hoverTween.Dispose();
            _spinner.Dispose();
        }

        base.Dispose(disposing);

        if (disposing)
        {
            _fontSync.Dispose();
            _iconFont?.Dispose();
            _iconFont = null;
        }
    }

    private enum SuButtonState
    {
        Normal,
        Hover,
        Pressed,
        Disabled,
    }

    private readonly struct SuButtonColors
    {
        public SuButtonColors(Color back, Color fore, Color border)
        {
            Back = back;
            Fore = fore;
            Border = border;
        }

        public Color Back { get; }

        public Color Fore { get; }

        public Color Border { get; }
    }
}
