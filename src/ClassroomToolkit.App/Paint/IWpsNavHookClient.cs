namespace ClassroomToolkit.App.Paint;

internal interface IWpsNavHookClient
{
    bool Available { get; }
    void SetInterceptEnabled(bool enabled);
    void SetBlockOnly(bool enabled);
    void SetInterceptKeyboard(bool enabled);
    void SetInterceptWheel(bool enabled);
    void SetEmitWheelOnBlock(bool enabled);
    Task<bool> StartAsync();
    void Stop();
}

internal sealed class WpsNavHookClient : IWpsNavHookClient
{
    private readonly WpsSlideshowNavigationHook _hook;

    public WpsNavHookClient(WpsSlideshowNavigationHook hook)
    {
        _hook = hook ?? throw new ArgumentNullException(nameof(hook));
    }

    public bool Available => _hook.Available;

    public void SetInterceptEnabled(bool enabled) => _hook.SetInterceptEnabled(enabled);
    public void SetBlockOnly(bool enabled) => _hook.SetBlockOnly(enabled);
    public void SetInterceptKeyboard(bool enabled) => _hook.SetInterceptKeyboard(enabled);
    public void SetInterceptWheel(bool enabled) => _hook.SetInterceptWheel(enabled);
    public void SetEmitWheelOnBlock(bool enabled) => _hook.SetEmitWheelOnBlock(enabled);
    public Task<bool> StartAsync() => _hook.StartAsync();
    public void Stop() => _hook.Stop();
}
