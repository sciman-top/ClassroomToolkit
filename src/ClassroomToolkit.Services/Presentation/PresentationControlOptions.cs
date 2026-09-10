using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.Services.Presentation;

public sealed class PresentationControlOptions
{
    public const int AutoFallbackFailureThresholdDefault = 2;
    public const int AutoFallbackProbeIntervalCommandsDefault = 8;

    public InputStrategy Strategy { get; set; } = InputStrategy.Auto;
    public bool WheelAsKey { get; set; }
    // 后台中继必须走窗口消息；SendInput 只能进入当前系统输入流，
    // 而且不能保证事件送达后台的放映窗口。
    public bool AllowBackground { get; set; }
    public bool AllowWps { get; set; } = true;
    public bool AllowOffice { get; set; } = true;
    public int WpsDebounceMs { get; set; } = 200;
    public bool LockStrategyWhenDegraded { get; set; } = true;
    public int AutoFallbackFailureThreshold { get; set; } = AutoFallbackFailureThresholdDefault;
    public int AutoFallbackProbeIntervalCommands { get; set; } = AutoFallbackProbeIntervalCommandsDefault;
}
