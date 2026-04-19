# 变更证据：缩小时界线附近页面闪烁（二次修复，2026-04-19）

- issue_id: photo-zoom-fullscreen-lag-flicker-boundary
- attempt_count: 2
- clarification_mode: direct_fix
- rule_ids: R1,R2,R3,R6,R8
- risk_level: medium
- scope:
  - src/ClassroomToolkit.App/Paint/CrossPageNeighborPageFramePolicy.cs
  - src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.Rendering.cs
  - src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.Helpers.cs
  - tests/ClassroomToolkit.Tests/CrossPageNeighborPageFramePolicyTests.cs

## Basis（根因）
- 缩小时，邻页槽位发生 remap 时若目标页位图尚未到达，原策略会直接 `CollapseSlot`，导致界线附近出现“先消失再重现”。
- 图片邻页预取成功后未主动触发刷新，目标位图就绪到显示存在等待窗口。

## Changes
1. 邻页页面帧决策策略新增受控开关 `preferHoldCurrentFrameOnSlotRemap`：
   - 默认行为不变（slot remap 且目标缺失仍可折叠）；
   - 仅在缩放交互窗口（且非墨迹操作）启用“连续性优先”，允许暂留当前槽位帧，避免瞬时空白闪烁。
2. 渲染路径将上述开关仅用于缩放交互场景：
   - `preferHoldCurrentFrameOnSlotRemap: zoomInteractionActive && !inkOperationActive`。
3. 图片邻页预取命中后主动请求一次跨页刷新：
   - `RequestCrossPageDisplayUpdate(CrossPageUpdateSources.WithImmediate(CrossPageUpdateSources.NeighborRender))`，缩短“位图已就绪但尚未显示”的空窗。
4. 增补策略单测：
   - 校验默认仍会在 slot remap 场景折叠；
   - 校验缩放连续性开关开启时会保留当前帧。

## Commands / Evidence
- `codex --version`
  - exit_code: 0
  - key_output: `codex-cli 0.121.0`
- `codex --help`
  - exit_code: 0
  - key_output: `Codex CLI usage/help`
- `codex status`
  - exit_code: 1
  - type: platform_na
  - reason: `stdin is not a terminal`（非交互终端）
  - alternative_verification: `codex --version` + `codex --help`
  - evidence_link: `docs/change-evidence/20260419-photo-zoom-seam-flicker-followup2.md`
  - expires_at: 2026-04-26T23:59:59+08:00

- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~CrossPageNeighborPageFramePolicyTests|FullyQualifiedName~CrossPageViewportBoundsDefaultsTests|FullyQualifiedName~CrossPageNeighborBitmapResolvePolicyTests"`
  - first_exit_code: 1
  - first_key_output: `MSB3027/MSB3021`（`sciman Classroom Toolkit (94104)` 锁定输出文件）
  - remediation: `Get-Process -Id 94104 | Stop-Process -Force`
  - final_exit_code: 0
  - final_key_output: `Passed 10, Failed 0`
- `dotnet build ClassroomToolkit.sln -c Debug`
  - exit_code: 0
  - key_output: `0 warnings, 0 errors`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - exit_code: 0
  - key_output: `Passed 3287, Failed 0`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - exit_code: 0
  - key_output: `Passed 28, Failed 0`

## Hotspot 人工复核
- 复核点 1：是否误把“旧页残影”扩散到非缩放场景。
  - 结论：连续性保留仅在 `zoomInteractionActive && !inkOperationActive` 启用，默认行为未放宽。
- 复核点 2：预取完成即刷新是否导致刷新风暴。
  - 结论：请求仍走现有跨页去重/节流通道（duplicate window + min interval），风险可控。
- 复核点 3：兼容性与契约。
  - 结论：未改外部契约与数据格式；build/test/contract 全通过。

## Rollback
- `git restore --source=HEAD -- src/ClassroomToolkit.App/Paint/CrossPageNeighborPageFramePolicy.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.Rendering.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.Helpers.cs tests/ClassroomToolkit.Tests/CrossPageNeighborPageFramePolicyTests.cs`
