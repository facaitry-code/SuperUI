namespace SuperUI.Rendering;

/// <summary>
/// 极简补间动画：驱动悬停/聚焦等过渡值（0..1），用 WinForms Timer 以 15ms 步进，缓出曲线。
/// </summary>
internal sealed class SuTween : IDisposable
{
    private readonly System.Windows.Forms.Timer _timer;
    private float _from;
    private float _to;
    private float _value;
    private int _duration = 130;
    private int _elapsed;

    public SuTween(float initial = 0f)
    {
        _value = initial;
        _from = initial;
        _to = initial;
        _timer = new System.Windows.Forms.Timer { Interval = 15 };
        _timer.Tick += OnTick;
    }

    /// <summary>当前值（0..1）。</summary>
    public float Value => _value;

    /// <summary>是否正在动画。</summary>
    public bool IsAnimating => _timer.Enabled;

    /// <summary>值变化（每帧触发一次）。</summary>
    public event EventHandler? Changed;

    /// <summary>立刻跳到指定值，不播放动画。</summary>
    public void Snap(float value)
    {
        _timer.Stop();
        _from = _to = _value = SuMath.Clamp(value, 0f, 1f);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>动画到目标值。</summary>
    public void To(float target, int duration = 130)
    {
        target = SuMath.Clamp(target, 0f, 1f);

        if (Math.Abs(_value - target) < 0.001f)
        {
            _timer.Stop();
            _to = target;
            return;
        }

        _from = _value;
        _to = target;
        _duration = Math.Max(16, duration);
        _elapsed = 0;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _elapsed += _timer.Interval;
        var progress = Math.Min(1f, _elapsed / (float)_duration);
        var eased = 1f - (1f - progress) * (1f - progress) * (1f - progress);
        _value = _from + (_to - _from) * eased;

        if (progress >= 1f)
        {
            _value = _to;
            _timer.Stop();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer.Dispose();
    }
}
