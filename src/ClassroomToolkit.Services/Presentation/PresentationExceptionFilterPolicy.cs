namespace ClassroomToolkit.Services.Presentation;

internal static class PresentationExceptionFilterPolicy
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
