namespace ClassroomToolkit.App.Windowing;

internal readonly record struct ExplicitForegroundRetouchRuntimeState(
    DateTime LastRetouchUtc)
{
    internal static ExplicitForegroundRetouchRuntimeState Default => new(
        LastRetouchUtc: WindowDedupDefaults.UnsetTimestampUtc);
}
