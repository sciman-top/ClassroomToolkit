# 全量代码审查与首轮高风险清扫 Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在不改变既有业务边界的前提下，完成一次可复现的全量代码审查并清扫首轮高风险缺陷，输出可留档证据与回滚点。

**Architecture:** 先用 CI 闸门确保“编译/依赖/全量测试”基线稳定，再按 `App-Windowing -> Interop -> Infra` 风险顺序执行“静态扫描 + 定向测试 + 缺陷修复 + 防回归测试”。所有修复遵循归宿边界，禁止把业务编排回堆到窗口热点文件。最终通过证据目录与人工回归清单闭环验收。

**Tech Stack:** .NET 10, WPF, xUnit, FluentAssertions, GitHub Actions, PowerShell, Coverlet (`XPlat Code Coverage`)。

---

## Preconditions And Source Of Truth

- I'm using the writing-plans skill to create the implementation plan.
- Baseline commit: `03b906c`（2026-03-18）
- 审查手册：`E:\PythonProject\ClassroomToolkit\docs\validation\2026-03-18-full-code-audit-playbook.md`
- CI 闸门：`E:\PythonProject\ClassroomToolkit\.github\workflows\quality-gate.yml`
- 人工回归清单：`E:\PythonProject\ClassroomToolkit\docs\validation\manual-final-regression-checklist.md`
- 回滚 Runbook：`E:\PythonProject\ClassroomToolkit\docs\runbooks\migration-rollback-playbook.md`

## File Structure

- Create: `E:\PythonProject\ClassroomToolkit\docs\validation\evidence\2026-03-18-full-audit\01-risk-inventory.md`
- Create: `E:\PythonProject\ClassroomToolkit\docs\validation\evidence\2026-03-18-full-audit\02-static-and-test-gates.md`
- Create: `E:\PythonProject\ClassroomToolkit\docs\validation\evidence\2026-03-18-full-audit\03-hotspot-findings.md`
- Create: `E:\PythonProject\ClassroomToolkit\docs\validation\evidence\2026-03-18-full-audit\04-fixes-and-regression.md`
- Modify if defects found: `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.cs`
- Modify if defects found: `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.cs`
- Modify if defects found: `src/ClassroomToolkit.App/Windowing/WindowInteropRetryExecutor.cs`
- Modify if defects found: `src/ClassroomToolkit.Interop/Presentation/KeyboardHook.cs`
- Modify if defects found: `src/ClassroomToolkit.Interop/Presentation/WpsSlideshowNavigationHook.cs`
- Modify if defects found: `src/ClassroomToolkit.Interop/Utilities/ComObjectManager.cs`
- Modify if defects found: `src/ClassroomToolkit.Infra/Storage/RollCallSqliteStoreAdapter.cs`
- Modify if defects found: `src/ClassroomToolkit.Infra/Storage/StudentWorkbookSqliteStoreAdapter.cs`
- Create when缺口确认: `tests/ClassroomToolkit.Tests/ComObjectManagerTests.cs`
- Create when缺口确认: `tests/ClassroomToolkit.Tests/CrossPageDelayExecutionHelperTests.cs`
- Create when缺口确认: `tests/ClassroomToolkit.Tests/PaintActionInvokerTests.cs`
- Test: `tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj`

## Chunk 1: Baseline Gates

### Task 1: 建立风险清单与审查批次

**Files:**
- Create: `E:\PythonProject\ClassroomToolkit\docs\validation\evidence\2026-03-18-full-audit\01-risk-inventory.md`

- [ ] **Step 1: 记录首轮高风险文件名单与风险类型**

按以下维度建表：文件路径、风险类型、现有测试、缺口、负责人、计划批次。  
首批文件至少包含：
`PaintOverlayWindow.Photo.CrossPage.cs`、`PaintOverlayWindow.Input.cs`、`PaintOverlayWindow.Ink.cs`、`ImageManagerWindow.xaml.cs`、`WindowInteropRetryExecutor.cs`、`KeyboardHook.cs`、`WpsSlideshowNavigationHook.cs`、`ComObjectManager.cs`、`RollCallSqliteStoreAdapter.cs`、`StudentWorkbookSqliteStoreAdapter.cs`。

- [ ] **Step 2: 检查基线工作区状态**

Run: `git status --short`  
Expected: 输出为空（工作区 clean）。

- [ ] **Step 3: 确认 CI 闸门文件存在**

Run: `Test-Path .github/workflows/quality-gate.yml`  
Expected: `True`

### Task 2: 执行静态闸门与全量自动化

**Files:**
- Create: `E:\PythonProject\ClassroomToolkit\docs\validation\evidence\2026-03-18-full-audit\02-static-and-test-gates.md`

- [ ] **Step 1: Restore + 漏洞扫描**

Run:
```powershell
dotnet restore ClassroomToolkit.sln --locked-mode
dotnet list ClassroomToolkit.sln package --vulnerable --include-transitive
```

Expected: restore 成功，漏洞扫描无阻断项。

- [ ] **Step 2: Debug/Release 构建闸门**

Run:
```powershell
dotnet build ClassroomToolkit.sln -c Debug --no-restore /warnaserror
dotnet build ClassroomToolkit.sln -c Release --no-restore /warnaserror
```

Expected: 两个配置均通过。

- [ ] **Step 3: 关键契约测试闸门**

Run:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
```

Expected: 全绿。

- [ ] **Step 4: Debug/Release 全量测试与覆盖率**

Run:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --collect:"XPlat Code Coverage" --results-directory artifacts/TestResults/Debug --logger "trx;LogFileName=debug.trx" --blame-hang --blame-hang-timeout 5m
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Release --no-build --results-directory artifacts/TestResults/Release --logger "trx;LogFileName=release.trx" --blame-hang --blame-hang-timeout 5m
```

Expected: 全绿且无 hang 报告。

## Chunk 2: Hotspot Review And Fixes

### Task 3: App/Windowing 热点深审

**Files:**
- Modify if defects found: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Paint\PaintOverlayWindow.Photo.CrossPage.cs`
- Modify if defects found: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Paint\PaintOverlayWindow.Input.cs`
- Modify if defects found: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Windowing\WindowInteropRetryExecutor.cs`
- Modify if defects found: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Photos\ImageManagerWindow.xaml.cs`
- Create when缺口确认: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\CrossPageDelayExecutionHelperTests.cs`
- Create when缺口确认: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\PaintActionInvokerTests.cs`
- Create: `E:\PythonProject\ClassroomToolkit\docs\validation\evidence\2026-03-18-full-audit\03-hotspot-findings.md`

- [ ] **Step 1: 逐文件检查并发/重入/阻塞点**

检查项：`async void`、`Task.Run` 使用边界、`Thread.Sleep`、UI 线程访问、重复订阅与取消订阅、重入防护。

- [ ] **Step 2: 为每个命中的问题写最小复现测试（先红）**

针对问题新建或扩展测试文件，先让测试失败，记录失败断言和复现条件。

- [ ] **Step 3: 仅在目标归宿模块做最小修复**

禁止把修复逻辑塞回 `MainWindow.*` 或 `PaintOverlayWindow.xaml.cs` 非归宿区域。

- [ ] **Step 4: 运行定向测试确认转绿**

Run:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~CrossPage|FullyQualifiedName~WindowInteropRetryExecutor|FullyQualifiedName~ImageManager|FullyQualifiedName~PaintActionInvoker"
```

Expected: 新增与既有相关测试均通过。

### Task 4: Interop/Infra 热点深审

**Files:**
- Modify if defects found: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.Interop\Presentation\KeyboardHook.cs`
- Modify if defects found: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.Interop\Presentation\WpsSlideshowNavigationHook.cs`
- Modify if defects found: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.Interop\Utilities\ComObjectManager.cs`
- Modify if defects found: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.Infra\Storage\RollCallSqliteStoreAdapter.cs`
- Modify if defects found: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.Infra\Storage\StudentWorkbookSqliteStoreAdapter.cs`
- Create when缺口确认: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\ComObjectManagerTests.cs`
- Modify if needed: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\InteropHookLifecycleContractTests.cs`
- Modify if needed: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\RollCallSqliteStoreAdapterTests.cs`
- Modify if needed: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\StudentWorkbookSqliteStoreAdapterTests.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\docs\validation\evidence\2026-03-18-full-audit\03-hotspot-findings.md`

- [ ] **Step 1: 审核 COM/Hook 生命周期闭环**

核对创建、回调、解绑、释放、异常路径清理是否成对且可重复调用。

- [ ] **Step 2: 审核 Interop 异常降级与重试**

重点核对 `RPC_E_CALL_REJECTED`、WPS 不可用、窗口句柄失效时的重试上限与降级提示。

- [ ] **Step 3: 审核数据持久化一致性**

确认 SQLite/Excel 回退路径不会破坏 `students.xlsx` 与设置兼容；异常后不会留下半写状态。

- [ ] **Step 4: 运行定向测试确认**

Run:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~InteropHook|FullyQualifiedName~GlobalHookService|FullyQualifiedName~ComObjectManager|FullyQualifiedName~RollCallSqliteStoreAdapter|FullyQualifiedName~StudentWorkbookSqliteStoreAdapter"
```

Expected: 相关测试全绿。

## Chunk 3: Closure And Evidence

### Task 5: 全量回归、人工验收与归档

**Files:**
- Create: `E:\PythonProject\ClassroomToolkit\docs\validation\evidence\2026-03-18-full-audit\04-fixes-and-regression.md`
- Modify: `E:\PythonProject\ClassroomToolkit\docs\handover.md`

- [ ] **Step 1: 重新运行全量测试**

Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`  
Expected: PASS

- [ ] **Step 2: 执行人工回归清单**

按 `docs/validation/manual-final-regression-checklist.md` 执行课堂场景，记录截图和日志路径。

- [ ] **Step 3: 写入缺陷闭环记录**

记录项：复现步骤、根因、修复、测试补丁、回滚点。

- [ ] **Step 4: 更新交接文档**

在 `docs/handover.md` 更新本轮审查结论、剩余风险和下一轮优先级。

## Natural Chunk Review Handoff

- Review Chunk 1: 验证基线闸门命令与产物目录完整。
- Review Chunk 2: 逐项确认“先测试后修复”与“不回堆热点文件”。
- Review Chunk 3: 验证证据链完整可追溯（命令、报告、截图、回滚点）。

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-03-18-full-code-audit-implementation-plan.md`. Ready to execute?
