namespace ClassroomToolkit.Interop.Presentation;

public sealed partial class WpsSlideshowNavigationHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;
    private const int HcAction = 0;

    private const int HookCallbackTimeoutMs = 50;
    private const int MaxHookRetries = 3;
    private const int ExceptionLogIntervalMs = 30000;

    private readonly HookProc _keyboardProc;
    private readonly HookProc _mouseProc;
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private bool _interceptEnabled;
    private bool _blockOnly;
    private bool _interceptKeyboard = true;
    private bool _interceptWheel = true;
    private bool _emitWheelOnBlock = true;
    private int _callbackExceptionCount;
    private long _lastExceptionLogTick;
    private int _dispatchGeneration;
    private volatile bool _disposed;
    public int LastError { get; private set; }
    private readonly HashSet<VirtualKey> _allowedKeys = new()
    {
        VirtualKey.Up,
        VirtualKey.Down,
        VirtualKey.Left,
        VirtualKey.Right,
        VirtualKey.PageUp,
        VirtualKey.PageDown,
        VirtualKey.Space,
        VirtualKey.Enter
    };

    public WpsSlideshowNavigationHook()
    {
        _keyboardProc = KeyboardProc;
        _mouseProc = MouseProc;
    }

    public event Action<int, string>? NavigationRequested;

    public bool Available => OperatingSystem.IsWindows();

    public void SetInterceptEnabled(bool enabled) => _interceptEnabled = enabled;

    public void SetBlockOnly(bool enabled) => _blockOnly = enabled;

    public void SetInterceptKeyboard(bool enabled) => _interceptKeyboard = enabled;

    public void SetInterceptWheel(bool enabled) => _interceptWheel = enabled;

    public void SetEmitWheelOnBlock(bool enabled) => _emitWheelOnBlock = enabled;
}
