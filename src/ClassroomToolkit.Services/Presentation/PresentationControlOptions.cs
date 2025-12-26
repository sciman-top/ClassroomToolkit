using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.Services.Presentation;

public sealed record PresentationControlOptions
{
    public InputStrategy Strategy { get; init; } = InputStrategy.Auto;
    public bool WheelAsKey { get; init; }
    public bool AllowWps { get; init; } = true;
    public bool AllowOffice { get; init; } = true;
}
