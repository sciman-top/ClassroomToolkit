namespace ClassroomToolkit.App.Windowing;

internal static class PhotoCloseOwnerDetachmentPolicy
{
    internal static bool ShouldDetachOwners(bool syncFloatingOwnersVisible)
    {
        return !syncFloatingOwnersVisible;
    }
}
