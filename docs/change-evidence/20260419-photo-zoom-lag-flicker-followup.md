# 变更证据：全屏 PDF/图片缩放卡顿与边界闪烁修复（2026-04-19）

- issue_id: photo-zoom-fullscreen-lag-flicker-boundary
- attempt_count: 1
- clarification_mode: direct_fix
- rule_ids: R1,R2,R6,R8
- risk_level: medium
- scope:
  - src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.Zoom.cs
  - src/ClassroomToolkit.App/Paint/CrossPageViewportBoundsDefaults.cs
  - tests/ClassroomToolkit.Tests/CrossPageViewportBoundsDefaultsTests.cs

## Basis（根因）
- 缩放链路在交互期仅同步已可见邻页 transform，但未实时触发跨页可见集重算，导致新进入视口的邻页更新滞后（体感为缩放不跟手、延迟明显）。
- 跨页可见判定边界余量仅 `2 DIP`，在缩小时于页面分界线附近容易发生“进/出可见区抖动”，触发短暂消隐再出现。

## Changes
1. 在 `ApplyPhotoScale` 的跨页路径中，保留邻页 transform 即时同步，同时补充：
   - `RequestCrossPageDisplayUpdate(CrossPageUpdateSources.ApplyScale)`
   - 使缩放中按现有节流机制持续重算跨页可见集与邻页布局，不再等到交互停稳后才补刷。
2. 将 `CrossPageViewportBoundsDefaults.VisibilityMarginDip` 从 `2.0` 提升到 `16.0`，增加边界滞回空间，降低分界线附近抖动闪烁。
3. 同步更新默认值测试断言。

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
  - evidence_link: `docs/change-evidence/20260419-photo-zoom-lag-flicker-followup.md`
  - expires_at: 2026-04-26T23:59:59+08:00

- `dotnet build ClassroomToolkit.sln -c Debug`
  - first_exit_code: 1
  - first_key_output: `MSB3027/MSB3021`（`sciman Classroom Toolkit` 进程锁定输出文件）
  - remediation: `Get-Process -Id 120116 | Stop-Process -Force`
  - final_exit_code: 0
  - final_key_output: `0 warnings, 0 errors`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - exit_code: 0
  - key_output: `Passed 3285, Failed 0`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - exit_code: 0
  - key_output: `Passed 28, Failed 0`

## Hotspot 人工复核
- 复核点 1：缩放时增加 `apply-scale` 请求是否导致过度刷新。
  - 结论：请求仍受现有重复窗口与最小间隔节流控制（interaction 路径），风险可控。
- 复核点 2：边界余量增大是否影响功能正确性。
  - 结论：仅扩大可见判定缓冲，不改变分页计算与边界夹紧语义；属于稳定性优化。
- 复核点 3：兼容与契约。
  - 结论：未改外部数据格式与对外契约；build/test/contract 全通过。

## Rollback
- `git restore --source=HEAD -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.Zoom.cs src/ClassroomToolkit.App/Paint/CrossPageViewportBoundsDefaults.cs tests/ClassroomToolkit.Tests/CrossPageViewportBoundsDefaultsTests.cs`
