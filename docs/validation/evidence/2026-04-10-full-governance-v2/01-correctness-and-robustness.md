# 2026-04-10 Full Governance v2 - Phase 1 Correctness And Robustness

## 1) Objective

- 在不改变外部行为和契约的前提下，执行第一批鲁棒性与去冗余收敛。
- 所有改动遵循固定门禁顺序：`build -> test -> contract/invariant -> hotspot`。

## 2) Changes

### Change A: Interop retry wait path hardening

- file: `src/ClassroomToolkit.App/Windowing/WindowInteropRetryExecutor.cs`
- action:
  - 统一 `WaitBeforeRetry` 到 `cancellationToken.WaitHandle.WaitOne(retrySleepMs)` 路径
  - 移除 `Thread.Sleep` 分支，减少不可中断阻塞实现分叉
- behavior impact:
  - 对外 API 与返回语义不变
  - 异常场景仍按 `ObjectDisposedException => false` 处理

### Change B: Add contract to prevent blocking regression

- file: `tests/ClassroomToolkit.Tests/App/WindowInteropRetryExecutorContractTests.cs`
- action:
  - 新增契约测试，约束源码必须使用 `WaitHandle.WaitOne`
  - 明确禁止回归为 `Thread.Sleep`

### Change C: De-dup retry policy logic (reduce overdesign duplication)

- new file: `src/ClassroomToolkit.App/Windowing/WindowInteropRetryPolicyCore.cs`
- modified files:
  - `src/ClassroomToolkit.App/Windowing/WindowTopmostInteropRetryPolicy.cs`
  - `src/ClassroomToolkit.App/Windowing/WindowStyleInteropRetryPolicy.cs`
  - `src/ClassroomToolkit.App/Windowing/WindowPlacementInteropRetryPolicy.cs`
- action:
  - 抽取三类窗口 interop 重试的公共判定核心（最大重试次数、无效句柄错误）
  - 各策略保持原有枚举和对外决策语义，仅把重复条件判断下沉到共享核心
- behavior impact:
  - 外部调用与测试期望不变
  - 维护点由 3 份重复逻辑收敛为 1 份核心逻辑

## 3) Verification Evidence

### Round 1 (before change set B/C)

1. `dotnet build ClassroomToolkit.sln -c Debug` -> PASS
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` -> PASS (`3211/3211`)
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"` -> PASS (`25/25`)
4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1` -> PASS

### Round 2 (after all changes)

1. `dotnet build ClassroomToolkit.sln -c Debug` -> PASS
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` -> PASS (`3212/3212`)
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"` -> PASS (`25/25`)
4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1` -> PASS

## 4) Risk Assessment

- risk_level: low
- rationale:
  - 仅涉及窗口 interop 重试内部实现与策略判定去重
  - 全量测试与契约门禁通过，未触发行为回归

## 5) Rollback

- 回滚文件：
  - `src/ClassroomToolkit.App/Windowing/WindowInteropRetryExecutor.cs`
  - `src/ClassroomToolkit.App/Windowing/WindowInteropRetryPolicyCore.cs`（新增）
  - `src/ClassroomToolkit.App/Windowing/WindowTopmostInteropRetryPolicy.cs`
  - `src/ClassroomToolkit.App/Windowing/WindowStyleInteropRetryPolicy.cs`
  - `src/ClassroomToolkit.App/Windowing/WindowPlacementInteropRetryPolicy.cs`
  - `tests/ClassroomToolkit.Tests/App/WindowInteropRetryExecutorContractTests.cs`（新增）
- 回滚后执行：
  1. `dotnet build ClassroomToolkit.sln -c Debug`
  2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  3. 契约过滤测试
  4. hotspot 脚本

