using System.ComponentModel;
using SuperUI.Theming;

namespace SuperUI.Controls;

/// <summary>
/// SuperUI 控件基类：开启双缓冲/抗锯齿绘制、自动登记主题订阅、统一字体来源。
/// </summary>
[ToolboxItem(false)]
public abstract class SuControlBase : Control, ISuThemed
{
    private readonly SuFontSync _fontSync;
    private bool _initializing = true;

    protected SuControlBase()
    {
        _fontSync = new SuFontSync(this);
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = SuThemeManager.Current.Background;
        SuThemeManager.Register(this);
        _initializing = false;
    }

    /// <summary>当前主题。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SuTheme Theme => SuThemeManager.Current;

    /// <summary>用户是否已自定义字体（为 true 时主题不再覆盖字体）。</summary>
    protected bool FontCustomized
    {
        get => _fontSync.Customized;
        set => _fontSync.Customized = value;
    }

    /// <inheritdoc />
    public virtual void OnThemeChanged()
    {
        ApplyTheme();
        Invalidate();
    }

    /// <summary>主题应用逻辑，子类重写以刷新自身颜色。</summary>
    protected virtual void ApplyTheme()
    {
        _fontSync.Apply(Theme);
        ForeColor = Theme.TextPrimary;
        BackColor = SuBackdrop.Resolve(this);
    }

    /// <summary>仅同步主题字体。</summary>
    protected void ApplyThemeFont() => _fontSync.Apply(Theme);

    /// <inheritdoc />
    protected override void OnParentBackColorChanged(EventArgs e)
    {
        base.OnParentBackColorChanged(e);
        if (_initializing)
        {
            return;
        }

        BackColor = SuBackdrop.Resolve(this);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        if (_initializing)
        {
            return;
        }

        BackColor = SuBackdrop.Resolve(this);
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
        }

        base.Dispose(disposing);

        if (disposing)
        {
            _fontSync.Dispose();
        }
    }
}
