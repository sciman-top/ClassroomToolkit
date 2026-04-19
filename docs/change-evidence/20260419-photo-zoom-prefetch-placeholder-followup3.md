# 变更证据：PDF/图片缩放卡顿与边界闪烁三次修复（2026-04-19）

- issue_id: photo-zoom-fullscreen-lag-flicker-boundary
- attempt_count: 3
- clarification_mode: direct_fix
- rule_ids: R1,R2,R3,R6,R8
- risk_level: medium
- scope:
  - src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Loading.cs
  - src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.Helpers.cs
  - src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.Rendering.cs

## Basis
- 用户仍观察到全屏缩小时页面边界附近短暂消失后重现。
- 根因补充 1：图片邻页预取原先通过 Dispatcher 执行，实际在 UI 线程同步解码，缩小时大量邻页进入视口会造成卡顿。
- 根因补充 2：上一轮“保留当前帧”只保留了 Source，没有把临时帧移动到目标槽位位置，目标页位图未就绪时仍可能留下空白。

## Changes
- `TryLoadBitmapSource` 增加可传入的 `targetDecodeWidth`，让后台预取不用访问窗口/显示器状态。
- 图片邻页预取改为 `SafeTaskRunner.Run` 后台解码，UI 线程只做缓存写入和刷新请求。
- 缩放期间目标页帧未就绪时，使用最近的已显示帧作为临时占位，并把占位帧移动到目标槽位；等目标页位图到达后再替换，不提前声明槽位身份。
- 保留现有跨页去重/节流通道，避免预取完成刷新造成刷新风暴。

## Commands / Evidence
- `dotnet build ClassroomToolkit.sln -c Debug`
  - exit_code: 0
  - key_output: `0 warnings, 0 errors`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~CrossPageNeighborPageFramePolicyTests|FullyQualifiedName~CrossPageViewportBoundsDefaultsTests|FullyQualifiedName~CrossPageNeighborBitmapResolvePolicyTests|FullyQualifiedName~CrossPageNeighborHeightResolvePolicyTests"`
  - exit_code: 0
  - key_output: `Passed 13, Failed 0`
- `codex --version`
  - exit_code: 0
  - key_output: `codex-cli 0.121.0`
- `codex --help`
  - exit_code: 0
  - key_output: `Codex CLI usage/help`
- `codex status`
  - exit_code: 1
  - type: platform_na
  - reason: `stdin is not a terminal`
  - alternative_verification: `codex --version` + `codex --help`
  - evidence_link: `docs/change-evidence/20260419-photo-zoom-prefetch-placeholder-followup3.md`
  - expires_at: 2026-04-26T23:59:59+08:00
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - exit_code: 0
  - key_output: `Passed 3287, Failed 0`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - exit_code: 0
  - key_output: `Passed 28, Failed 0`

## Hotspot Review
- 预取线程边界：后台只创建并 Freeze `BitmapSource`，缓存字典仍在 UI 线程写入。
- 临时占位边界：只在缩放交互且非墨迹操作时使用，不改变目标页身份，避免目标位图到达后被误判为已显示。
- 风险：极短时间可能看到相邻页临时占位内容，但不再出现空白消失；目标位图就绪后会替换。

## Rollback
- `git restore --source=HEAD -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Loading.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.Helpers.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.Rendering.cs`
