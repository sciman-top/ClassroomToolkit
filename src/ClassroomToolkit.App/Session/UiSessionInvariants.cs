using System.Collections.Generic;

namespace ClassroomToolkit.App.Session;

internal static class UiSessionInvariants
{
    public static IReadOnlyList<string> Validate(UiSessionState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var violations = new List<string>();

        if (state.Scene != UiSceneKind.Idle)
        {
            if (state.OverlayTopmostRequired != UiSessionOverlayVisibilityPolicy.IsOverlayTopmostRequired(state.Scene))
            {
                violations.Add("INV-001: 非 Idle 场景必须要求 OverlayTopmostRequired=true。");
            }
            var widgetsVisible = UiSessionOverlayVisibilityPolicy.AreFloatingWidgetsVisible(state.Scene);
            if (state.RollCallVisible != widgetsVisible
                || state.LauncherVisible != widgetsVisible
                || state.ToolbarVisible != widgetsVisible)
            {
                violations.Add("INV-001: 非 Idle 场景下工具条/点名/启动器可见性必须为 true。");
            }
        }

        if (state.ToolMode == UiToolMode.Draw)
        {
            var expectedNavigation = UiSessionNavigationPolicy.Resolve(state.Scene, state.ToolMode);
            if (state.NavigationMode != expectedNavigation)
            {
                violations.Add("INV-002: Draw 模式下导航必须满足：放映场景=HookOnly，其它场景=Disabled。");
            }
            var expectedInkVisibility = UiSessionInkVisibilityPolicy.Resolve(state.Scene, state.ToolMode);
            if (state.InkVisibility != expectedInkVisibility)
            {
                violations.Add("INV-003: Draw 模式下墨迹必须可编辑。");
            }
        }
        else
        {
            var expectedInkVisibility = UiSessionInkVisibilityPolicy.Resolve(state.Scene, state.ToolMode);
            if (state.InkVisibility != expectedInkVisibility && state.Scene == UiSceneKind.Idle)
            {
                violations.Add("INV-004: Cursor + Idle 时墨迹必须隐藏。");
            }
            if (state.InkVisibility != expectedInkVisibility && state.Scene != UiSceneKind.Idle)
            {
                violations.Add("INV-005: Cursor + 非 Idle 时墨迹必须只读可见。");
            }
        }

        if (state.Scene == UiSceneKind.Whiteboard && state.NavigationMode != UiNavigationMode.Disabled)
        {
            violations.Add("INV-006: Whiteboard 场景下导航必须禁用。");
        }

        var expectedFocusOwner = UiSessionFocusOwnerPolicy.Resolve(state.Scene);
        if (state.FocusOwner != expectedFocusOwner)
        {
            violations.Add($"INV-007: {state.Scene} 场景焦点所有者应为 {expectedFocusOwner}。");
        }

        return violations;
    }
}
