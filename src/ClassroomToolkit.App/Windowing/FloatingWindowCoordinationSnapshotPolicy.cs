namespace ClassroomToolkit.App.Windowing;

internal static class FloatingWindowCoordinationSnapshotPolicy
{
    public static FloatingWindowCoordinationSnapshot Resolve(
        FloatingWindowRuntimeSnapshot runtime,
        LauncherWindowRuntimeSnapshot launcher,
        bool toolbarVisible,
        bool rollCallVisible,
        bool toolbarActive,
        bool rollCallActive,
        bool imageManagerActive,
        bool launcherActive,
        bool toolbarOwnerAlreadyOverlay,
        bool rollCallOwnerAlreadyOverlay,
        bool imageManagerOwnerAlreadyOverlay)
    {
        return new FloatingWindowCoordinationSnapshot(
            Runtime: runtime,
            Launcher: launcher,
            TopmostVisibility: FloatingTopmostVisibilitySnapshotPolicy.Resolve(
                toolbarVisible: toolbarVisible,
                rollCallVisible: rollCallVisible,
                launcherVisible: runtime.LauncherVisible,
                imageManagerVisible: runtime.ImageManagerVisible,
                overlayVisible: runtime.OverlayVisible),
            UtilityActivity: FloatingUtilityActivitySnapshotPolicy.Resolve(
                toolbarActive: toolbarActive,
                rollCallActive: rollCallActive,
                imageManagerActive: imageManagerActive,
                launcherActive: launcherActive),
            Owner: FloatingOwnerRuntimeSnapshotPolicy.Resolve(
                overlayVisible: runtime.OverlayVisible,
                toolbarOwnerAlreadyOverlay: toolbarOwnerAlreadyOverlay,
                rollCallOwnerAlreadyOverlay: rollCallOwnerAlreadyOverlay,
                imageManagerOwnerAlreadyOverlay: imageManagerOwnerAlreadyOverlay));
    }
}
