using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.Services.Presentation;

public sealed record PresentationControlPlan(
    PresentationType TargetType,
    InputStrategy Strategy,
    bool UseWheelAsKey);
