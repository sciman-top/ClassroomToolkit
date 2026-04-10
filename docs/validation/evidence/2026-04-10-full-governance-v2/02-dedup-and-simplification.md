# 2026-04-10 Full Governance v2 - Phase 2 Dedup And Simplification

## 1) Objective

- 收敛重复策略实现，降低维护成本与行为漂移风险。
- 保持对外行为、测试契约与门禁结果不变。

## 2) Executed Simplification

### 2.1 Windowing interop retry policy dedup

- new:
  - `src/ClassroomToolkit.App/Windowing/WindowInteropRetryPolicyCore.cs`
- refactored:
  - `src/ClassroomToolkit.App/Windowing/WindowTopmostInteropRetryPolicy.cs`
  - `src/ClassroomToolkit.App/Windowing/WindowStyleInteropRetryPolicy.cs`
  - `src/ClassroomToolkit.App/Windowing/WindowPlacementInteropRetryPolicy.cs`

### 2.2 What was removed

- 重复常量定义（`MaxRetryAttempts/ErrorInvalidWindowHandle/ErrorInvalidHandle`）从三处策略移除。
- 重复判定分支（最大重试、无效句柄、默认可重试）集中到单一核心实现。

## 3) Validation Evidence

- 全量测试：`3212/3212` PASS
- 契约/不变量过滤：`25/25` PASS
- hotspot 预算：PASS
- 关键检索证据：
  - `rg -n "MaxRetryAttempts = WindowInteropRetryDefaults.MaxRetryAttempts|ErrorInvalidWindowHandle = WindowInteropRetryDefaults.ErrorInvalidWindowHandle|ErrorInvalidHandle = WindowInteropRetryDefaults.ErrorInvalidHandle" src/ClassroomToolkit.App`
  - 输出仅剩 `WindowInteropRetryPolicyCore.cs`

## 4) Residual Risk

- 风险级别：低
- 说明：
  - 各对外 `Resolve/ShouldRetry` API 未改变
  - 各策略枚举类型与 reason 语义保持不变

## 5) Next Dedup Candidates

后续批次建议优先扫描：
1. `Paint` 子域 `*Policy/*.Defaults/*.Coordinator` 中输入输出等价策略族。
2. `Infra/Storage` 多适配器重复异常降级模板。
3. `App/Photos` 多分部类中的相同调度与异常兜底模板。

