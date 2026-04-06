using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.Services.Presentation;

public sealed class PresentationControlOptions
{
    public const int AutoFallbackFailureThresholdDefault = 2;
    public const int AutoFallbackProbeIntervalCommandsDefault = 8;

    public InputStrategy Strategy { get; set; } = InputStrategy.Auto;
    public bool WheelAsKey { get; set; }
    public bool AllowWps { get; set; } = true;
    public bool AllowOffice { get; set; } = true;
    public int WpsDebounceMs { get; set; } = 200;
    public bool LockStrategyWhenDegraded { get; set; } = true;
    public int AutoFallbackFailureThreshold { get; set; } = AutoFallbackFailureThresholdDefault;
    public int AutoFallbackProbeIntervalCommands { get; set; } = AutoFallbackProbeIntervalCommandsDefault;
}
