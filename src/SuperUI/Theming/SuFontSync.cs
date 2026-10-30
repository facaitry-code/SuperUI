using System.Drawing;

namespace SuperUI.Theming;

/// <summary>
/// 把主题字体同步到控件：用户一旦手动设置 Font，就不再被主题覆盖。
/// </summary>
internal sealed class SuFontSync
{
    private readonly Control _owner;
    private Font? _themeFont;
    private bool _syncing;

    public SuFontSync(Control owner) => _owner = owner;

    /// <summary>用户是否已自定义字体。</summary>
    public bool Customized { get; set; }

    public void Apply(SuTheme theme)
    {
        if (Customized)
        {
            return;
        }

        Font font;
        try
        {
            font = theme.CreateFont();
        }
        catch (ArgumentException)
        {
            // 主题字体族在当前系统不存在，保持原字体。
            return;
        }

        var previous = _themeFont;
        _syncing = true;
        try
        {
            _owner.Font = font;
        }
        finally
        {
            _syncing = false;
        }

        // Font.Equals 是“按属性相等”比较：属性一致时 WinForms 会沿用旧实例，
        // 此时新字体没人引用，可以安全释放；绝不能释放旧实例。
        if (ReferenceEquals(_owner.Font, previous))
        {
            font.Dispose();
            return;
        }

        _themeFont = font;
        if (previous is not null && !ReferenceEquals(previous, font))
        {
            previous.Dispose();
        }
    }

    public void OnFontChanged()
    {
        if (_syncing)
        {
            return;
        }

        if (!ReferenceEquals(_owner.Font, _themeFont))
        {
            Customized = true;
        }
    }

    public void Dispose()
    {
        var font = _themeFont;
        _themeFont = null;
        font?.Dispose();
    }
}
