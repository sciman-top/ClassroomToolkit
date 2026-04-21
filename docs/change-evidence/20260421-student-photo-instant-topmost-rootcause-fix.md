# 20260421-student-photo-instant-topmost-rootcause-fix

- date: 2026-04-21
- issue_id: student-photo-layering-front-surface
- attempt_count: 4
- clarification_mode: clarify_required
- clarification_scenario: bugfix
- clarification_questions:
  1. “是否仍然是首张照片先压住工具条/启动器/点名窗口，再被抢回？”
- clarification_answers:
  1. 是，仍有“瞬时压层”。
- risk_level: medium

## 1) 边界与归宿（R1）
- 边界：仅修复“首张学生照片显示瞬时压层”问题，不改点名业务、计时、名单与资源格式。
- 当前落点：
  - `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml`
  - `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml.cs`
  - `src/ClassroomToolkit.App/Windowing/RollCallAuxOverlayTopmostPolicy.cs`
- 目标归宿：工具条/点名窗口/启动器始终在最前，学生照片层稳定位于其下一层，不再出现首帧抢前台。

## 2) 根因复盘（R3）
- 真正触发点不是“retouch 是否触发”，而是“照片窗本身仍被定义为 topmost”：
  1. `PhotoOverlayWindow.xaml` 使用 `Topmost="True"`。
  2. `EnsureOverlayVisible()` 里继续执行 `WindowTopmostExecutor.ApplyNoActivate(... enabled: true ...)`。
- 结果：首张照片首次 `Show()` 时，会先进入 topmost 前列，随后才被工具条/启动器/点名窗口重排抢回；后续连续点名时因为这些窗口已在前列，所以现象不明显。

## 3) 变更清单
1. 照片窗口默认层级改为非 topmost：
   - `PhotoOverlayWindow.xaml`：`Topmost="True" -> "False"`。
2. 照片窗口显示时不再提权到 topmost：
   - `PhotoOverlayWindow.xaml.cs`：`WindowTopmostExecutor.ApplyNoActivate(this, enabled: false, enforceZOrder: false)`。
3. 点名辅助叠加策略明确照片层永不 topmost：
   - `RollCallAuxOverlayTopmostPolicy.Resolve(...)`：`PhotoOverlayTopmost = false`，`PhotoOverlayEnforceZOrder = false`。
4. 契约测试同步：
   - `PhotoOverlayTopmostNoActivateContractTests`
   - `RollCallAuxOverlayTopmostPolicyTests`

## 4) 验证（R6）

### 4.1 平台最小诊断矩阵（B.2）
1. cmd: `codex --version`
   - exit_code: 0
   - key_output: `codex-cli 0.122.0`
2. cmd: `codex --help`
   - exit_code: 0
   - key_output: 正常输出命令帮助
3. cmd: `codex status`
   - exit_code: 1
   - key_output: `Error: stdin is not a terminal`

### 4.2 定向回归
1. cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoOverlayTopmostNoActivateContractTests|FullyQualifiedName~RollCallAuxOverlayTopmostPolicyTests|FullyQualifiedName~MainWindowImmediateZOrderRetouchContractTests|FullyQualifiedName~PhotoOverlayShowOrderContractTests"`
   - exit_code: 0
   - key_output: `Passed: 11, Failed: 0`

### 4.3 硬门禁顺序（build -> test -> contract/invariant -> hotspot）
1. cmd: `dotnet build ClassroomToolkit.sln -c Debug`
   - exit_code: 0
   - key_output: `0 Warning(s), 0 Error(s)`
2. cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
   - exit_code: 0
   - key_output: `Passed: 3394, Failed: 0`
3. cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - exit_code: 0
   - key_output: `Passed: 28, Failed: 0`
4. hotspot 人工复核
   - 复核点：
     - `PhotoOverlayWindow` 首显路径（`Show()` 前后 topmost 状态）
     - `RollCallAuxOverlayTopmostPolicy` 对照片层的长期策略
     - `MainWindow.RequestImmediateFloatingZOrderRetouch()` 的同步抢层回补
   - 结论：照片层不再具备 topmost 提权入口；工具窗 topmost 仍保留，满足“工具窗始终最前，照片下一层”。

## 5) N/A
- type: `platform_na`
- reason: `codex status` 在当前非交互终端不可执行（`stdin is not a terminal`）。
- alternative_verification: 使用 `codex --version`、`codex --help` 与完整门禁结果补证。
- evidence_link: `docs/change-evidence/20260421-student-photo-instant-topmost-rootcause-fix.md`
- expires_at: `2026-05-21`

## 6) 回滚动作
1. 回滚 `PhotoOverlayWindow.xaml` 的 `Topmost="False"`。
2. 回滚 `PhotoOverlayWindow.xaml.cs` 中 `ApplyNoActivate(... enabled: false ...)`。
3. 回滚 `RollCallAuxOverlayTopmostPolicy` 的 `PhotoOverlayTopmost = false`。
4. 回滚后按 `build -> test -> contract/invariant -> hotspot` 重跑。
