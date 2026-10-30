namespace SuperUI.Forms;

/// <summary>
/// 主题化消息弹窗的统一入口：一行调用代替 <see cref="MessageBox"/>，
/// 外观完全由 <see cref="Theming.SuTheme"/> 决定（圆角卡片、语义图标、主题按钮）。
/// <para>
/// 三个快捷方法对应三种语义：<see cref="Info(string, string?, IWin32Window?)"/>（提示）、
/// <see cref="Warning(string, string?, IWin32Window?)"/>（警告）、
/// <see cref="Error(string, string?, IWin32Window?)"/>（错误）；
/// 需要「确定 / 取消」「是 / 否」时用
/// <see cref="Show(string, SuMessageBoxKind, string?, SuMessageBoxButtons, IWin32Window?, bool?)"/>
/// 或 <see cref="Confirm(string, string?, IWin32Window?)"/>。
/// </para>
/// </summary>
/// <example>
/// <code>
/// SuMessageBox.Info("已保存 12 条记录。");
/// SuMessageBox.Warning(this, "磁盘只剩 210 MB。", "空间不足");
/// SuMessageBox.Error(ex);                                        // 异常 → 错误弹窗
/// if (SuMessageBox.Confirm("确定要删除这 3 行吗？")) { Delete(); }  // 是 / 否
/// </code>
/// </example>
public static class SuMessageBox
{
    /// <summary>信息提示（主色 i 图标，只有「确定」）。</summary>
    public static DialogResult Info(string message, string? title = null, IWin32Window? owner = null)
        => Show(message, SuMessageBoxKind.Info, title, SuMessageBoxButtons.Ok, owner);

    /// <summary>信息提示，并显式指定父窗口（居中于父窗口）。</summary>
    public static DialogResult Info(IWin32Window? owner, string message, string? title = null)
        => Show(message, SuMessageBoxKind.Info, title, SuMessageBoxButtons.Ok, owner);

    /// <summary>警告（琥珀色三角 ! 图标，只有「确定」）。</summary>
    public static DialogResult Warning(string message, string? title = null, IWin32Window? owner = null)
        => Show(message, SuMessageBoxKind.Warning, title, SuMessageBoxButtons.Ok, owner);

    /// <summary>警告，并显式指定父窗口。</summary>
    public static DialogResult Warning(IWin32Window? owner, string message, string? title = null)
        => Show(message, SuMessageBoxKind.Warning, title, SuMessageBoxButtons.Ok, owner);

    /// <summary>错误（危险色 × 图标，只有「确定」）。</summary>
    public static DialogResult Error(string message, string? title = null, IWin32Window? owner = null)
        => Show(message, SuMessageBoxKind.Error, title, SuMessageBoxButtons.Ok, owner);

    /// <summary>错误，并显式指定父窗口。</summary>
    public static DialogResult Error(IWin32Window? owner, string message, string? title = null)
        => Show(message, SuMessageBoxKind.Error, title, SuMessageBoxButtons.Ok, owner);

    /// <summary>把异常变成错误弹窗：正文取 <see cref="Exception.Message"/>，标题默认取异常类型名。</summary>
    public static DialogResult Error(Exception exception, IWin32Window? owner = null, string? title = null)
    {
        if (exception is null)
        {
            throw new ArgumentNullException(nameof(exception));
        }

        return Show(
            exception.Message,
            SuMessageBoxKind.Error,
            title ?? exception.GetType().Name,
            SuMessageBoxButtons.Ok,
            owner);
    }

    /// <summary>是 / 否确认框：选「是」返回 true（等价于 <c>MessageBox.Show(...) == DialogResult.Yes</c>）。</summary>
    public static bool Confirm(string message, string? title = null, IWin32Window? owner = null)
        => Show(message, SuMessageBoxKind.Info, title ?? "请确认", SuMessageBoxButtons.YesNo, owner)
           == DialogResult.Yes;

    /// <summary>是 / 否确认框，并显式指定父窗口。</summary>
    public static bool Confirm(IWin32Window? owner, string message, string? title = null)
        => Confirm(message, title, owner);

    /// <summary>
    /// 通用弹窗：语义、标题、按钮组合、父窗口、是否遮罩全部可指定。
    /// <paramref name="scrim"/> 为 <c>null</c> 时沿用弹窗默认值（有 owner 就盖遮罩）。
    /// </summary>
    public static DialogResult Show(
        string message,
        SuMessageBoxKind kind = SuMessageBoxKind.Info,
        string? title = null,
        SuMessageBoxButtons buttons = SuMessageBoxButtons.Ok,
        IWin32Window? owner = null,
        bool? scrim = null)
    {
        using var dialog = new SuMessageBoxDialog
        {
            Message = message ?? string.Empty,
            Kind = kind,
            ButtonSet = buttons,
            Text = string.IsNullOrWhiteSpace(title) ? DefaultTitle(kind) : title!,

            // owner 是控件时直接拿它当遮罩宿主：ShowDialog(owner) 不一定把 Owner 属性设上。
            ScrimHost = owner as Control,
        };

        if (scrim.HasValue)
        {
            dialog.ShowScrim = scrim.Value;
        }

        // 有 owner 时居中于 owner（StartPosition = CenterParent），没有就落在屏幕中央。
        return owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
    }

    /// <summary>各语义的默认标题：提示 / 注意 / 错误。</summary>
    public static string DefaultTitle(SuMessageBoxKind kind) => kind switch
    {
        SuMessageBoxKind.Warning => "注意",
        SuMessageBoxKind.Error => "错误",
        _ => "提示",
    };
}
