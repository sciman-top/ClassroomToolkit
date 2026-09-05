using ClassroomToolkit.Domain;

namespace ClassroomToolkit.Services.Presentation;

internal static class PresentationExceptionFilterPolicy
{
    internal static bool IsNonFatal(Exception ex) => DomainExceptionFilterPolicy.IsNonFatal(ex);
}
