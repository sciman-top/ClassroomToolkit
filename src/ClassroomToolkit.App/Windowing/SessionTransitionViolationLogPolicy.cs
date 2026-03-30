namespace ClassroomToolkit.App.Windowing;

internal static class SessionTransitionViolationLogPolicy
{
    internal static bool ShouldLog(int violationCount)
    {
        return violationCount > 0;
    }
}
