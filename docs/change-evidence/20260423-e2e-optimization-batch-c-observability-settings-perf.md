# 20260423-e2e-optimization-batch-c-observability-settings-perf

## Meta
- issue_id: `e2e-opt-20260423-batch-c`
- clarification_mode: `direct_fix`
- risk_level: `low_to_medium`
- scope: `settings oversize observability + settings-load performance sampling + release preflight integration`

## Problem
1. `JsonSettingsDocumentStoreAdapter` 的尺寸防护缺少可观测计数，不利于上线后趋势追踪与告警阈值配置。
2. 缺少可重复执行的 `settings.json` 加载冷/热路径采样入口，发布前无法产出标准化性能样本工件。
3. `preflight-check.ps1` 尚未覆盖 settings-load 采样步骤。

## Changes

### 1) Settings 超限拒绝可观测性增强
- file: `src/ClassroomToolkit.Infra/Settings/JsonSettingsDocumentStoreAdapter.cs`
- change:
  - 新增 `OversizedSettingsRejectCount` 静态计数器与公开只读属性。
  - 将尺寸校验从“单一 false”拆分为“校验异常 vs 真正超限”两条路径。
  - 超限场景写入结构化 Debug 诊断：`oversized-settings-rejected ... rejectCount=...`。
- benefit:
  - 可直接用于运行态诊断与后续阈值治理。
  - 避免把 I/O 校验异常误判为超限。

### 2) Settings 加载性能采样链路
- files:
  - `scripts/validation/SettingsLoadPerfProbe/SettingsLoadPerfProbe.csproj`
  - `scripts/validation/SettingsLoadPerfProbe/Program.cs`
  - `scripts/validation/collect-settings-load-performance-samples.ps1`
- change:
  - 新增轻量 probe：对 small/medium settings 文件采集 cold/hot 迭代耗时，输出 p50/p95/avg/max。
  - 新增统一采样脚本，产出 JSON + Markdown 工件（`artifacts/validation`）。
- benefit:
  - 形成可重复的性能基线采样入口，可用于发布前性能回归比较。

### 3) 发布前校验接入
- file: `scripts/release/preflight-check.ps1`
- change:
  - 新增步骤 `settings-load-performance-sampling`。
  - 新增开关 `-SkipSettingsLoadPerformanceSampling`。
- benefit:
  - 发布前自动生成 settings-load 性能样本，减少人工遗漏。

### 4) 运行手册更新
- file: `docs/runbooks/governance-endstate-maintenance.md`
- change:
  - 增加 settings-load 采样命令示例。

### 5) 测试补强
- file: `tests/ClassroomToolkit.Tests/JsonSettingsDocumentStoreAdapterTests.cs`
- change:
  - 在超限加载测试中增加对 `OversizedSettingsRejectCount` 递增断言。

## Verification
1. 定向测试：
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~JsonSettingsDocumentStoreAdapterTests"`
- exit: `0` (`14/14`)

2. 性能采样脚本：
- `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/validation/collect-settings-load-performance-samples.ps1 -Configuration Debug -OutputRoot artifacts/validation`
- exit: `0`
- artifact:
  - `artifacts/validation/settings-load-performance-small-*.json`
  - `artifacts/validation/settings-load-performance-medium-*.json`
  - `artifacts/validation/settings-load-performance-summary-*.json/.md`

3. 硬门禁链：
- `dotnet build ClassroomToolkit.sln -c Debug` -> `exit=0`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` -> `exit=0` (`3430/3430`)
- `dotnet test ...contract filter...` -> `exit=0` (`28/28`)
- `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1` -> `exit=0`

4. 质量门：
- `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile quick -Configuration Debug`
- exit: `0` (`ALL PASS`)

5. 发布预检：
- `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/release/preflight-check.ps1 -Configuration Debug -SkipTests -SkipCompatibilityReport`
- exit: `0` (`build + ui-performance-sampling + settings-load-performance-sampling ALL PASS`)

## Issue Discovered & Fixed During Verification
- issue: `collect-settings-load-performance-samples.ps1` 在接收绝对路径 `OutputRoot` 时重复拼接仓库根导致路径非法。
- fix: 增加 rooted path 分支，绝对路径直接使用，相对路径再拼接仓库根。
- regression check: 复跑 `preflight-check` 已通过。

## Risk / Rollback
- risk tier:
  - `low`: preflight 脚本与 runbook 更新。
  - `low_to_medium`: settings 尺寸校验路径细化与拒绝计数新增。
- rollback:
  - `git revert <this-commit>`
  - 或按文件回滚：
    - `src/ClassroomToolkit.Infra/Settings/JsonSettingsDocumentStoreAdapter.cs`
    - `tests/ClassroomToolkit.Tests/JsonSettingsDocumentStoreAdapterTests.cs`
    - `scripts/validation/SettingsLoadPerfProbe/*`
    - `scripts/validation/collect-settings-load-performance-samples.ps1`
    - `scripts/release/preflight-check.ps1`
    - `docs/runbooks/governance-endstate-maintenance.md`
- rollback verify:
  - `build -> test -> contract/invariant -> hotspot -> quality-gates`
