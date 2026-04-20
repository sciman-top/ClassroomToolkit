namespace ClassroomToolkit.Interop.Presentation;

public sealed partial class KeyboardHook : IDisposable
{
    private const int WhKeyboardLl = 13;

    // Performance monitoring
    private const int HookCallbackTimeoutMs = 50;
    private const int ExceptionLogIntervalMs = 30000;

    private readonly HookProc _hookProc;
    private IntPtr _hookId;
    private bool _leftShiftDown;
    private bool _rightShiftDown;
    private bool _leftCtrlDown;
    private bool _rightCtrlDown;
    private bool _leftAltDown;
    private bool _rightAltDown;
    private VirtualKey? _pendingSuppressedKey;
    private int _callbackExceptionCount;
    private long _lastExceptionLogTick;
    private volatile bool _acceptEvents;
    private volatile bool _disposed;

    public bool IsActive => _hookId != IntPtr.Zero;

    public int LastError { get; private set; }

    public KeyboardHook()
    {
        _hookProc = HookCallback;
    }

    public KeyBinding? TargetBinding { get; set; }

    public bool SuppressWhenMatched { get; set; }

    public event Action<KeyBinding>? BindingTriggered;
}
