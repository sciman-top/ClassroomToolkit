namespace ClassroomToolkit.App.Windowing;

internal static class FloatingUtilityActivitySnapshotPolicy
{
    public static FloatingUtilityActivitySnapshot Resolve(
        bool toolbarActive,
        bool rollCallActive,
        bool imageManagerActive,
        bool launcherActive)
    {
        return new FloatingUtilityActivitySnapshot(
            ToolbarActive: toolbarActive,
            RollCallActive: rollCallActive,
            ImageManagerActive: imageManagerActive,
            LauncherActive: launcherActive);
    }
}
