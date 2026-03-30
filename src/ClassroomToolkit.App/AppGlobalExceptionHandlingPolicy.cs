namespace ClassroomToolkit.App;

internal enum AppGlobalExceptionAction
{
    LogOnly,
    NotifyUser
}

internal readonly record struct AppGlobalExceptionHandlingDecision(
    bool IsFatal,
    bool ShouldMarkDispatcherHandled,
    AppGlobalExceptionAction Action);

internal static class AppGlobalExceptionHandlingPolicy
{
    internal static bool IsFatal(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception is OutOfMemoryException
            or AppDomainUnloadedException
            or BadImageFormatException
            or CannotUnloadAppDomainException
            or InvalidProgramException
            or StackOverflowException
            or AccessViolationException;
    }

    internal static bool IsNonFatal(Exception exception)
    {
        return !IsFatal(exception);
    }

    internal static AppGlobalExceptionHandlingDecision ResolveForDispatcher(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (IsFatal(exception))
        {
            return new AppGlobalExceptionHandlingDecision(
                IsFatal: true,
                ShouldMarkDispatcherHandled: false,
                Action: AppGlobalExceptionAction.LogOnly);
        }

        return new AppGlobalExceptionHandlingDecision(
            IsFatal: false,
            ShouldMarkDispatcherHandled: true,
            Action: AppGlobalExceptionAction.NotifyUser);
    }

    internal static AppGlobalExceptionHandlingDecision ResolveForBackground(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return new AppGlobalExceptionHandlingDecision(
            IsFatal: IsFatal(exception),
            ShouldMarkDispatcherHandled: false,
            Action: AppGlobalExceptionAction.LogOnly);
    }
}
