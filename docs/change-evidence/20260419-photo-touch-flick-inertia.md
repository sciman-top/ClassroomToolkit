# 2026-04-19 Photo Touch Flick Inertia

- rule_id: `R1/R2/R6/R8`
- risk_level: `medium`
- scope:
  - `src/ClassroomToolkit.App/Paint/*`
  - `tests/ClassroomToolkit.Tests/*`
- current_landing: `PaintOverlayWindow` photo pan / manipulation input path
- target_destination: touch-first single-finger flick with shared inertia core

## Basis

- 设计规格：`docs/superpowers/specs/2026-04-19-photo-touch-flick-inertia-design.md`
- 实现计划：`docs/superpowers/plans/2026-04-19-photo-touch-flick-inertia.md`

## Changes

- 新增 `PhotoTouchInteractionPolicy`，把单指触摸归属、双指 manipulation zoom、promoted-touch stylus 过滤从原有 stylus/manipulation 逻辑中拆出。
- 新增 `PhotoPanPointerKind / PhotoPanReleaseTuning / PhotoPanReleaseTuningPolicy`，把触摸释放速度阈值、减速度、最大位移帧钳制接入共享惯性内核。
- `PaintOverlayWindow.Input.Manipulation.cs` 改为显式按 manipulation 事件触点数决定是否接管，移除旧的隐式双指兼容入口。
- 新增 `PaintOverlayWindow.Input.Touch.cs`，让单指触摸直接复用 `BeginPhotoPan -> UpdatePhotoPan -> EndPhotoPan` 流程，并在再次按下时立即刹停当前惯性。
- `PaintOverlayWindow.Input.Stylus.cs` 对 promoted touch stylus 直接短路，避免同一根手指同时驱动 touch/stylus 两条输入路径。
- `PaintOverlayWindow.Photo.Transform.PanInertia.cs` 根据 `_photoPanActivePointerKind` 选择释放调参；鼠标/触控笔保持原基线，触摸获得更低起速阈值和更长惯性。

## Commands

1. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoTouchInteractionPolicyTests|FullyQualifiedName~PhotoInputAlignmentPolicyTests|FullyQualifiedName~PhotoManipulationAdmissionPolicyTests"`
   - result: `Passed`
   - key_output: `57 passed, 0 failed`
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoPanReleaseTuningPolicyTests|FullyQualifiedName~PhotoPanInertiaMotionPolicyTests"`
   - result: `Passed`
   - key_output: `15 passed, 0 failed`
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoTouchInputContractTests|FullyQualifiedName~PhotoPanInertiaRenderingContractTests|FullyQualifiedName~PhotoTouchInteractionPolicyTests|FullyQualifiedName~PhotoInputAlignmentPolicyTests|FullyQualifiedName~PhotoManipulationAdmissionPolicyTests|FullyQualifiedName~PhotoPanReleaseTuningPolicyTests|FullyQualifiedName~PhotoPanInertiaMotionPolicyTests"`
   - result: `Passed`
   - key_output: `75 passed, 0 failed`
4. `dotnet build ClassroomToolkit.sln -c Debug`
   - result: `Passed`
   - key_output: `0 warnings, 0 errors`
5. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
   - result: `Passed`
   - key_output: `3321 passed, 0 failed`
6. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - result: `Passed`
   - key_output: `28 passed, 0 failed`

## Hotspot Review

- reviewed_files:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Touch.cs`
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Manipulation.cs`
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.PanInertia.cs`
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Stylus.cs`
  - `src/ClassroomToolkit.App/Paint/PhotoPanInertiaMotionPolicy.cs`
- findings: `none`
- conclusions:
  - single-touch now enters the custom pan pipeline directly instead of relying on manipulation translation.
  - manipulation routing now fails closed for one-touch gestures and only admits zoom-capable multi-touch.
  - inertia startup and decay use pointer-kind-aware release tuning, so touch can flick farther without changing mouse baseline.

## Residual Risks

- 当前验证以策略测试、合同测试和全量回归为主，没有真实触屏设备上的交互录制回放；触摸手感仍需一体机实机确认。
- `ManipulationInertiaStarting/Completed` 对触点数采用 continuation 语义（最少按双指处理），这是为了保证已获准的多指手势在抬手后能正常完成，不用于单指准入。

## Rollback

- restore command:
  - `git restore --source=HEAD -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Manipulation.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Stylus.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Touch.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Lifecycle.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.PanInertia.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.State.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs src/ClassroomToolkit.App/Paint/PhotoInputAlignmentPolicy.cs src/ClassroomToolkit.App/Paint/PhotoManipulationAdmissionPolicy.cs src/ClassroomToolkit.App/Paint/PhotoPanInertiaMotionPolicy.cs src/ClassroomToolkit.App/Paint/PhotoPanReleaseTuning.cs src/ClassroomToolkit.App/Paint/PhotoPanReleaseTuningPolicy.cs src/ClassroomToolkit.App/Paint/PhotoTouchInteractionPolicy.cs tests/ClassroomToolkit.Tests/PhotoInputAlignmentPolicyTests.cs tests/ClassroomToolkit.Tests/PhotoManipulationAdmissionPolicyTests.cs tests/ClassroomToolkit.Tests/PhotoPanInertiaMotionPolicyTests.cs tests/ClassroomToolkit.Tests/PhotoPanInertiaRenderingContractTests.cs tests/ClassroomToolkit.Tests/PhotoPanReleaseTuningPolicyTests.cs tests/ClassroomToolkit.Tests/PhotoTouchInputContractTests.cs tests/ClassroomToolkit.Tests/PhotoTouchInteractionPolicyTests.cs docs/change-evidence/20260419-photo-touch-flick-inertia.md`
