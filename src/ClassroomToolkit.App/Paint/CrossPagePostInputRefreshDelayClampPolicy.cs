namespace ClassroomToolkit.App.Paint;

internal static class CrossPagePostInputRefreshDelayClampPolicy
{
    internal const int MinDelayMs = 40;
    internal const int MaxDelayMs = 400;

    internal static int Clamp(int delayMs)
    {
        return Math.Clamp(delayMs, MinDelayMs, MaxDelayMs);
    }
}
