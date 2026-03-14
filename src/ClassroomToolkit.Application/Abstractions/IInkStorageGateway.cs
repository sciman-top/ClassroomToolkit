namespace ClassroomToolkit.Application.Abstractions;

public interface IInkStorageGateway
{
    int CleanupOrphanSidecarsInDirectory(string directory);
    int CleanupOrphanCompositeOutputsInDirectory(string directory);
}
