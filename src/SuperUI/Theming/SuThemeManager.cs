namespace SuperUI.Theming;

/// <summary>
/// 全局主题管理器：一处调用 <see cref="Apply(SuTheme)"/>，所有已注册的 SuperUI 控件即时换肤。
/// 内部使用弱引用登记控件，控件释放后不会残留。
/// </summary>
public static class SuThemeManager
{
    private static readonly List<WeakReference<ISuThemed>> Targets = new();
    private static SuTheme _current = SuThemePresets.Light.Clone();

    /// <summary>当前生效的主题（只读快照，已克隆）。</summary>
    public static SuTheme Current => _current;

    /// <summary>主题切换后触发，供普通 WinForms 控件（Label/Panel 等）自行同步颜色。</summary>
    public static event EventHandler? ThemeChanged;

    /// <summary>应用一个主题（内部会克隆，避免外部共享实例被修改）。</summary>
    public static void Apply(SuTheme theme)
    {
        if (theme is null)
        {
            throw new ArgumentNullException(nameof(theme));
        }

        _current = theme.Clone();
        NotifyTargets();
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>按预设名称应用主题，例如 "Light" / "Dark" / "Ocean" / "Grape" / "Forest"。</summary>
    public static void Apply(string presetName)
    {
        var preset = SuThemePresets.Find(presetName)
                     ?? throw new ArgumentException($"未知主题名称：{presetName}", nameof(presetName));
        Apply(preset);
    }

    /// <summary>深/浅色二选一。</summary>
    public static void SetDark(bool dark) => Apply(dark ? SuThemePresets.Dark : SuThemePresets.Light);

    /// <summary>在浅色与深色之间切换（会重置为内置预设配色）。</summary>
    public static void ToggleLightDark() => Apply(Current.IsDark ? SuThemePresets.Light : SuThemePresets.Dark);

    /// <summary>登记一个主题订阅者。</summary>
    public static void Register(ISuThemed target)
    {
        if (target is null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        Prune();
        foreach (var reference in Targets)
        {
            if (reference.TryGetTarget(out var existing) && ReferenceEquals(existing, target))
            {
                return;
            }
        }

        Targets.Add(new WeakReference<ISuThemed>(target));
    }

    /// <summary>注销主题订阅者。</summary>
    public static void Unregister(ISuThemed target)
    {
        for (var i = Targets.Count - 1; i >= 0; i--)
        {
            if (!Targets[i].TryGetTarget(out var existing) || ReferenceEquals(existing, target))
            {
                Targets.RemoveAt(i);
            }
        }
    }

    private static void NotifyTargets()
    {
        var snapshot = Targets.ToArray();
        foreach (var reference in snapshot)
        {
            if (reference.TryGetTarget(out var target))
            {
                try
                {
                    target.OnThemeChanged();
                }
                catch (ObjectDisposedException)
                {
                    // 控件正在释放，忽略。
                }
            }
        }

        Prune();
    }

    private static void Prune()
    {
        for (var i = Targets.Count - 1; i >= 0; i--)
        {
            if (!Targets[i].TryGetTarget(out _))
            {
                Targets.RemoveAt(i);
            }
        }
    }
}
