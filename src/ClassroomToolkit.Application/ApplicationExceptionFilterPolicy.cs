namespace ClassroomToolkit.Application;

internal static class ApplicationExceptionFilterPolicy
{
    internal static bool IsNonFatal(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        return ex is not (
            OutOfMemoryException
            or AppDomainUnloadedException
            or BadImageFormatException
            or CannotUnloadAppDomainException
            or InvalidProgramException
            or StackOverflowException
            or AccessViolationException);
    }
}
