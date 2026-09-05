using ClassroomToolkit.Domain;

namespace ClassroomToolkit.Infra;

internal static class InfraExceptionFilterPolicy
{
    internal static bool IsNonFatal(Exception ex) => DomainExceptionFilterPolicy.IsNonFatal(ex);
}
