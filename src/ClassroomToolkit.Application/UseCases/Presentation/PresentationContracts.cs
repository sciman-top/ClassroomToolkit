namespace ClassroomToolkit.Application.UseCases.Presentation;

public enum PresentationCommand
{
    Next = 0,
    Previous = 1,
    First = 2,
    Last = 3,
    BlackScreenToggle = 4,
    WhiteScreenToggle = 5
}

public sealed record PresentationControlOptions(
    bool AllowWps = true,
    bool AllowOffice = true,
    bool WheelAsKey = false,
    int WpsDebounceMs = 200,
    bool LockStrategyWhenDegraded = true,
    int AutoFallbackFailureThreshold = 2,
    int AutoFallbackProbeIntervalCommands = 8);

public readonly record struct PresentationTarget(nint Handle);
