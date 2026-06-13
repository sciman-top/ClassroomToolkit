namespace ClassroomToolkit.App.Windowing;

internal readonly record struct RollCallAuxOverlayTopmostPlan(
    bool PhotoOverlayTopmost,
    bool PhotoOverlayEnforceZOrder,
    bool GroupOverlayTopmost,
    bool GroupOverlayEnforceZOrder);

internal static class RollCallAuxOverlayTopmostPolicy
{
    internal static RollCallAuxOverlayTopmostPlan Resolve(
        bool photoOverlayVisible,
        bool groupOverlayVisible,
        bool enforceZOrder)
    {
        return new RollCallAuxOverlayTopmostPlan(
            PhotoOverlayTopmost: photoOverlayVisible,
            // 学生照片需要进入 topmost band，才能稳定压住普通焦点窗口；
            // 主窗口会随后重排工具条/启动器/点名窗口，让它们继续位于照片上方。
            PhotoOverlayEnforceZOrder: enforceZOrder,
            GroupOverlayTopmost: groupOverlayVisible,
            GroupOverlayEnforceZOrder: enforceZOrder);
    }
}
