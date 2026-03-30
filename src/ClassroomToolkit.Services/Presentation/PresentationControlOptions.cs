using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.Services.Presentation;

public sealed class PresentationControlOptions
{
    public InputStrategy Strategy { get; set; } = InputStrategy.Auto;
    public bool WheelAsKey { get; set; }
    public bool AllowWps { get; set; } = true;
    public bool AllowOffice { get; set; } = true;
    public int WpsDebounceMs { get; set; } = 200;
    public bool LockStrategyWhenDegraded { get; set; } = true;
}
