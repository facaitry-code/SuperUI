using System.ComponentModel;
using SuperUI.Theming;

namespace SuperUI.Controls;

/// <summary>标签的语义色（决定文字颜色）。</summary>
public enum SuLabelTone
{
    /// <summary>主文字色。</summary>
    Primary,

    /// <summary>次要文字色。</summary>
    Secondary,

    /// <summary>占位/提示色。</summary>
    Placeholder,

    /// <summary>主色（强调）。</summary>
    Accent,

    /// <summary>危险/错误色。</summary>
    Danger,
}

/// <summary>
/// SuperUI 标签：在原生 <see cref="Label"/> 基础上跟随主题字体与语义色，
/// 保留 AutoSize / AutoEllipsis / TextAlign 等原生能力。
/// </summary>
[Description("SuperUI 标签：文字颜色取自主题（Primary/Secondary/Placeholder/Accent/Danger），跟随全局换肤。")]
[DefaultProperty(nameof(Text))]
public class SuLabel : Label, ISuThemed
{
    private readonly SuFontSync _fontSync;
    private SuLabelTone _tone = SuLabelTone.Primary;
    private bool _initializing = true;

    public SuLabel()
    {
        _fontSync = new SuFontSync(this);
        UseMnemonic = false;
        AutoSize = true;
        SuThemeManager.Register(this);
        ApplyTheme();
        _initializing = false;
    }

    /// <summary>语义色。</summary>
    [Category("SuperUI")]
    [DefaultValue(SuLabelTone.Primary)]
    public SuLabelTone Tone
    {
        get => _tone;
        set
        {
            if (_tone == value)
            {
                return;
            }

            _tone = value;
            ApplyTheme();
            Invalidate();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SuTheme Theme => SuThemeManager.Current;

    /// <inheritdoc />
    public virtual void OnThemeChanged()
    {
        ApplyTheme();
        Invalidate();
    }

    private void ApplyTheme()
    {
        _fontSync.Apply(Theme);
        ForeColor = ResolveColor(Theme);
        BackColor = SuBackdrop.Resolve(this);
    }

    private Color ResolveColor(SuTheme theme)
    {
        if (!Enabled)
        {
            return theme.TextDisabled;
        }

        return _tone switch
        {
            SuLabelTone.Secondary => theme.TextSecondary,
            SuLabelTone.Placeholder => theme.TextPlaceholder,
            SuLabelTone.Accent => theme.Primary,
            SuLabelTone.Danger => theme.Danger,
            _ => theme.TextPrimary,
        };
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (_initializing || _fontSync is null)
        {
            return;
        }

        _fontSync.OnFontChanged();
    }

    /// <inheritdoc />
    protected override void OnParentBackColorChanged(EventArgs e)
    {
        base.OnParentBackColorChanged(e);
        if (!_initializing)
        {
            BackColor = SuBackdrop.Resolve(this);
        }
    }

    /// <inheritdoc />
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        ForeColor = ResolveColor(Theme);
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
