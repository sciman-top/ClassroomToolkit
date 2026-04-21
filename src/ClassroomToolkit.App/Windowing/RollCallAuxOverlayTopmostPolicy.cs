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
            PhotoOverlayTopmost: false,
            // 学生照片层必须位于工具条/启动器/点名窗口下一层，不参与强制重排。
            PhotoOverlayEnforceZOrder: false,
            GroupOverlayTopmost: groupOverlayVisible,
            GroupOverlayEnforceZOrder: enforceZOrder);
    }
}
