namespace ClassroomToolkit.App.Paint;

internal static class InkPersistenceTogglePolicy
{
    internal static bool ShouldLoadPersistedInk(bool allowDiskFallback, bool inkSaveEnabled)
    {
        return allowDiskFallback && inkSaveEnabled;
    }

    internal static bool ShouldTrackWal(bool inkSaveEnabled)
    {
        return inkSaveEnabled;
    }

    internal static bool ShouldRecoverWal(bool inkSaveEnabled)
    {
        return inkSaveEnabled;
    }

    internal static bool ShouldRetainRuntimeCacheOnPhotoExit(bool inkSaveEnabled)
    {
        return inkSaveEnabled;
    }
}
