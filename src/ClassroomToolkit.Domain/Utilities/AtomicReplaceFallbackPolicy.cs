namespace ClassroomToolkit.Domain.Utilities;

public static class AtomicReplaceFallbackPolicy
{
    public static bool ShouldFallback(Exception exception)
    {
        return exception is UnauthorizedAccessException
            || exception is PlatformNotSupportedException;
    }
}
