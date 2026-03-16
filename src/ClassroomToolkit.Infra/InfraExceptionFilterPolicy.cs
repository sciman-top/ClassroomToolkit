namespace ClassroomToolkit.Infra;

internal static class InfraExceptionFilterPolicy
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
