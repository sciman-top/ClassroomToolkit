# 20260421-photo-zorder-flicker-topmost-layering

- 日期: 2026-04-21
- issue_id: photo-zorder-flicker-topmost-layering
- 风险等级: 中（窗口层级策略行为调整）

## 1) 规则落点与目标归宿（R1）
- 边界: 仅调整照片层与工具条/点名窗口/启动器的前后台层级协调逻辑，不改业务数据与配置格式。
- 当前落点: `src/ClassroomToolkit.App/Windowing/FloatingWindowExecutionExecutor.cs` 与对应测试。
- 目标归宿: 工具条/点名窗口/启动器稳定保持在照片层之上，照片层不再通过强制 z-order replay 抢前台。

## 2) 变更清单
- 代码:
  - `src/ClassroomToolkit.App/Windowing/FloatingWindowExecutionExecutor.cs`
    - 将 `ReplayOverlayBelowFloatingUtilities` 的 overlay replay 从 `enforceZOrder=true` 调整为 `false`。
- 测试:
  - `tests/ClassroomToolkit.Tests/App/FloatingWindowExecutionExecutorTests.cs`
    - 同步断言，确认 replay 触发但不做强制 z-order 重排。

## 3) 执行命令与证据（build -> test -> contract/invariant -> hotspot）

### 3.1 平台最小诊断矩阵（B.2）
1. cmd: `codex --version`
   - exit_code: 0
   - key_output: `codex-cli 0.122.0`
   - timestamp: `2026-04-21T22:55:10+08:00`
2. cmd: `codex --help`
   - exit_code: 0
   - key_output: 正常输出 help 命令列表
   - timestamp: `2026-04-21T22:55:11+08:00`
3. cmd: `codex status`
   - exit_code: 1
   - key_output: `Error: stdin is not a terminal`
   - timestamp: `2026-04-21T22:55:12+08:00`

### 3.2 硬门禁
1. cmd: `dotnet build ClassroomToolkit.sln -c Debug`
   - exit_code: 0
   - key_output: `0 Error(s)`
   - timestamp: `2026-04-21T22:56:20+08:00`
2. cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
   - exit_code: 0
   - key_output: `Passed: 3386, Failed: 0`
   - timestamp: `2026-04-21T22:57:00+08:00`
3. cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - exit_code: 0
   - key_output: `Passed: 28, Failed: 0`
   - timestamp: `2026-04-21T22:57:20+08:00`
4. hotspot 人工复核
   - 范围: `FloatingWindowExecutionExecutor` replay 分支与调用顺序（owner -> activation -> overlay replay -> topmost plan）
   - 结论: replay 仍保留（保障 overlay topmost 属性），但去除强制重排，避免与工具窗强制置顶形成抖动竞争；工具窗置顶执行仍完整保留。

## 4) N/A 记录

### platform_na-1
- type: `platform_na`
- reason: `codex status` 在当前非交互终端无法执行（`stdin is not a terminal`）。
- alternative_verification: 使用 `codex --version` 与 `codex --help` 验证 CLI 可用性与命令面。
- evidence_link: `docs/change-evidence/20260421-photo-zorder-flicker-topmost-layering.md#31-平台最小诊断矩阵b2`
- expires_at: `2026-05-21`

## 5) 回滚动作
1. 回滚代码:
   - 将 `FloatingWindowExecutionExecutor` 中 replay 调用第三参从 `false` 恢复为 `true`。
2. 回滚测试:
   - 将 `FloatingWindowExecutionExecutorTests` 的对应断言从 `BeFalse()` 恢复 `BeTrue()`。
3. 回滚后重新执行门禁顺序:
   - `build -> test -> contract/invariant -> hotspot`
