# 20260421-student-photo-first-frame-zorder-retouch

- date: 2026-04-21
- issue_id: student-photo-layering-front-surface
- attempt_count: 3
- clarification_mode: clarify_required
- clarification_scenario: bugfix
- clarification_questions:
  1. “是否还存在第一张照片先压住工具条/启动器/点名窗口，再被抢回？”
- clarification_answers:
  1. 是，仅首张（或重新开始点名后的首张）会先压层。
- risk_level: medium

## 1) 边界与归宿（R1）
- 边界：仅修复“首张学生照片显示瞬间”的层级抖动，不改照片数据流、定时关闭逻辑与点名业务流程。
- 当前落点：
  - `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml.cs`
  - `src/ClassroomToolkit.App/MainWindow.ZOrder.cs`
- 目标归宿：照片窗口首次 `Show()` 的同一链路内立刻触发主窗口强制 z-order 重排，避免首帧压住工具条/启动器/点名窗口。

## 2) 根因（补充）
- 之前已移除学生照片窗口的持续提权后，仍有“首张瞬时压层”：
  - 首张照片路径是异步解码后首次 `Show()`，在这次 `Show()` 到下一次全局重排（watchdog/交互触发）之间存在短暂窗口，照片层可能暂居 topmost 前列。
- 实质问题：缺少“首显即刻重排”动作，导致首帧窗口期可见。

## 3) 变更
1. 主窗口新增即刻重排入口：
   - `MainWindow.RequestImmediateFloatingZOrderRetouch()` -> `RequestApplyZOrderPolicy(forceEnforceZOrder: true)`
2. 学生照片窗口在 `EnsureOverlayVisible()` 中：
   - 记录 `becameVisible`（仅首次显示为 true）
   - 若首次显示，立即调用 `MainWindow.RequestImmediateFloatingZOrderRetouch()`
   - 该调用发生在同一 UI 链路，不等待 watchdog。

## 4) 验证（R6）

### 4.1 平台最小诊断矩阵（B.2）
1. cmd: `codex --version`
   - exit_code: 0
   - key_output: `codex-cli 0.122.0`
2. cmd: `codex --help`
   - exit_code: 0
   - key_output: help 正常输出
3. cmd: `codex status`
   - exit_code: 1
   - key_output: `Error: stdin is not a terminal`

### 4.2 定向回归
1. cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoOverlayTopmostNoActivateContractTests|FullyQualifiedName~MainWindowImmediateZOrderRetouchContractTests|FullyQualifiedName~RollCallAuxOverlayTopmostPolicyTests|FullyQualifiedName~PhotoOverlayShowOrderContractTests"`
   - exit_code: 0
   - key_output: `Passed: 10, Failed: 0`

### 4.3 硬门禁顺序
1. cmd: `dotnet build ClassroomToolkit.sln -c Debug`
   - exit_code: 0
   - key_output: `0 Error(s)`
2. cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
   - exit_code: 0
   - key_output: `Passed: 3393, Failed: 0`
3. cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - exit_code: 0
   - key_output: `Passed: 28, Failed: 0`
4. hotspot 人工复核：
   - 复核点：`PhotoOverlayWindow.EnsureOverlayVisible` 的调用时序与 `MainWindow` 强制重排入口。
   - 结论：首显时序已补齐“立刻重排”，不会等待后续定时/交互触发。

## 5) N/A
- type: `platform_na`
- reason: `codex status` 在非交互终端不可用（`stdin is not a terminal`）。
- alternative_verification: `codex --version` + `codex --help` + 门禁全通过。
- evidence_link: `docs/change-evidence/20260421-student-photo-first-frame-zorder-retouch.md`
- expires_at: `2026-05-21`

## 6) 回滚
1. 回滚 `MainWindow.ZOrder.cs` 中 `RequestImmediateFloatingZOrderRetouch()`。
2. 回滚 `PhotoOverlayWindow.xaml.cs` 中 `becameVisible` 与 `RequestImmediateFloatingZOrderRetouch()` 调用。
3. 回滚后按 `build -> test -> contract/invariant -> hotspot` 重跑。
