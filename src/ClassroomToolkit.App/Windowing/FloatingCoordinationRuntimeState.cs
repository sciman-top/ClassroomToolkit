namespace ClassroomToolkit.App.Windowing;

internal readonly record struct FloatingCoordinationRuntimeState(
    ZOrderSurface? LastFrontSurface,
    FloatingTopmostPlan? LastTopmostPlan)
{
    internal static FloatingCoordinationRuntimeState Default => new(
        LastFrontSurface: null,
        LastTopmostPlan: null);
}
