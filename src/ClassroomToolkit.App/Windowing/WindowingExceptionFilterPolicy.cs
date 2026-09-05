using ClassroomToolkit.Domain;

namespace ClassroomToolkit.App.Windowing;

internal static class WindowingExceptionFilterPolicy
{
    internal static bool IsNonFatal(Exception ex) => DomainExceptionFilterPolicy.IsNonFatal(ex);
}
