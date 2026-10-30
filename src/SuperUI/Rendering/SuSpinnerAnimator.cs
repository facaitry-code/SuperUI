namespace SuperUI.Rendering;

/// <summary>驱动旋转指示器角度的定时器（约 30fps）。</summary>
internal sealed class SuSpinnerAnimator : IDisposable
{
    private readonly System.Windows.Forms.Timer _timer;
    private readonly float _step;

    public SuSpinnerAnimator(float step = 14f, int interval = 33)
    {
        _step = step;
        _timer = new System.Windows.Forms.Timer { Interval = interval };
        _timer.Tick += OnTick;
    }

    /// <summary>当前起始角度（度）。</summary>
    public float Angle { get; private set; }

    /// <summary>是否正在旋转。</summary>
    public bool IsRunning => _timer.Enabled;

    /// <summary>每帧回调（用于触发重绘）。</summary>
    public event EventHandler? Tick;

    public void Start()
    {
        if (!_timer.Enabled)
        {
            _timer.Start();
        }
    }

    public void Stop() => _timer.Stop();

    private void OnTick(object? sender, EventArgs e)
    {
        Angle = (Angle + _step) % 360f;
        Tick?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer.Dispose();
    }
}
