# 20260421-student-photo-layering-rootcause-fix

- date: 2026-04-21
- issue_id: student-photo-layering-front-surface
- attempt_count: 2
- clarification_mode: clarify_required
- clarification_scenario: bugfix
- clarification_questions:
  1. “是否仍然是学生照片在最前台？”
  2. “目标是否是工具条/启动器/点名窗口始终压在学生照片之上？”
- clarification_answers:
  1. 是，仍在最前台。
  2. 是，且要求彻底根因审查。
- risk_level: medium

## 1) 边界与归宿（R1）
- 边界：只修复“学生照片窗口（RollCall 照片叠加层）”与工具条/启动器/点名窗口的前后台层级关系。
- 当前落点：
  - `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml.cs`
  - `src/ClassroomToolkit.App/RollCallWindow.Windowing.cs`
  - `src/ClassroomToolkit.App/Windowing/RollCallAuxOverlayTopmostPolicy.cs`
- 目标归宿：学生照片不抢前台；工具条/启动器/点名窗口保持最前；照片位于下一层。

## 2) 根因审计（Root Cause）
1. 修复对象偏差：上轮主要修了 `PaintOverlayWindow` 链路，但“学生照片展示”实际在 `Photos/PhotoOverlayWindow`。
2. 学生照片窗口主动前台抢占：
   - `PhotoOverlayWindow` 构造函数 `ShowActivated = true`，显示时会激活窗口。
   - `EnsureOverlayVisible()` 里 `WindowTopmostExecutor.ApplyNoActivate(... enforceZOrder: true)` 会强制重排到 topmost 前列。
3. 主窗口重排链路反向提权：
   - `MainWindow.EnsureCriticalFloatingWindowsTopmost()` 最后调用 `RollCallWindow.RetouchAuxOverlayWindowsTopmost(strictEnforceZOrder)`。
   - 原实现对 `_photoOverlay` 使用 `enforceZOrder` 原样传递（常为 `true`），导致学生照片在每次重排尾部被再次抬到最前。

## 3) 变更（Changes）
1. `PhotoOverlayWindow` 改为不抢激活：
   - `ShowActivated = false`
2. `PhotoOverlayWindow` 改为不强制 topmost 重排：
   - `EnsureOverlayVisible()` 中 `enforceZOrder: true -> false`
3. 新增策略 `RollCallAuxOverlayTopmostPolicy`：
   - 学生照片层 `PhotoOverlayEnforceZOrder` 固定为 `false`
   - 组名覆盖层保持既有 `enforceZOrder` 行为
4. `RollCallWindow.RetouchAuxOverlayWindowsTopmost` 改为使用该策略，避免学生照片在全局重排时被再提权。

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
1. cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~RollCallAuxOverlayTopmostPolicyTests|FullyQualifiedName~PhotoOverlayTopmostNoActivateContractTests|FullyQualifiedName~PhotoOverlayShowOrderContractTests|FullyQualifiedName~PhotoOverlayCloseHideGuardContractTests"`
   - exit_code: 0
   - key_output: `Passed: 9, Failed: 0`

### 4.3 硬门禁顺序
1. cmd: `dotnet build ClassroomToolkit.sln -c Debug`
   - exit_code: 0
   - key_output: `0 Error(s)`
2. cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
   - exit_code: 0
   - key_output: `Passed: 3391, Failed: 0`
3. cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - exit_code: 0
   - key_output: `Passed: 28, Failed: 0`
4. hotspot 人工复核：
   - 复核文件：
     - `PhotoOverlayWindow.xaml.cs`
     - `RollCallWindow.Windowing.cs`
     - `RollCallAuxOverlayTopmostPolicy.cs`
   - 结论：学生照片层已从“主动抢前台 + 重排链路再提权”双路径中移除；工具窗保留强制重排能力。

## 5) N/A 记录
- type: `platform_na`
- reason: `codex status` 在非交互终端不可用（`stdin is not a terminal`）。
- alternative_verification: `codex --version` + `codex --help` + 全部门禁通过。
- evidence_link: `docs/change-evidence/20260421-student-photo-layering-rootcause-fix.md`
- expires_at: `2026-05-21`

## 6) 回滚
1. `PhotoOverlayWindow.xaml.cs`
   - `ShowActivated = false` 回滚为 `true`
   - `enforceZOrder: false` 回滚为 `true`
2. `RollCallWindow.Windowing.cs`
   - 回滚到直接调用 `WindowTopmostExecutor.ApplyNoActivate(_photoOverlay, photoVisible, enforceZOrder)`
3. 删除：`RollCallAuxOverlayTopmostPolicy.cs` 与对应新增测试。
4. 回滚后重跑门禁顺序：`build -> test -> contract/invariant -> hotspot`。
