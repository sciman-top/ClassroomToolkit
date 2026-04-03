规则ID=R1/R2/R4/R6/R8
影响模块=src/ClassroomToolkit.App/Paint, tests/ClassroomToolkit.Tests, scripts/quality
当前落点=ink/photo 性能平滑与跨页一致性
目标归宿=跨页与平移缩放高频场景流畅且无串页
迁移批次=20260403-1
风险等级=中
执行命令=1) dotnet build ClassroomToolkit.sln -c Debug; 2) dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug; 3) dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"; 4) powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=focused regression PASS(49/49); contract/invariant PASS; hotspot PASS; continuous update 后全量 test PASS
回滚动作=按文内 Rollback 小节回退阈值/sidecar 校验/ArrayPool 改动并复验门禁

# 20260403-ink-photo-perf-smoothness

- rule_id: R1/R2/R4/R6/R8
- risk_level: medium
- scope: src/ClassroomToolkit.App/Paint + tests/ClassroomToolkit.Tests
- owner: codex
- date: 2026-04-03

## Goal
提升笔迹缓存/重绘、跨页同步与图片/PDF平移缩放阶段的连续流畅性，并增强跨页异步加载的页面隔离，避免笔迹误入其他页面。

## Changes
1. 降低跨页刷新节流阈值：
   - `CrossPageDisplayUpdateMinIntervalThresholds`: `56/42/30 -> 24/20/16` ms。
2. 降低平移触发阈值：
   - `PhotoPanInteractiveRefreshPolicy` 默认阈值 `1.25 -> 0.75` dip。
   - `InkRuntimeTimingDefaults.PhotoPanRedrawThresholdDip` `6.0 -> 3.0` dip。
3. 防串页：
   - `ScheduleNeighborInkSidecarLoad` 增加 `expectedCacheKey` 一致性校验；不一致直接 skip 并诊断 `stale-cache-key`。
4. 减少高频位图复制分配：
   - `TryCopyBitmapToRasterSurface` 改为 `ArrayPool<byte>.Shared` 租借/归还缓冲，降低 GC 抖动风险。
5. 测试更新：
   - 新增/更新跨页阈值、默认阈值、sidecar stale-key 合约断言。

## Commands And Evidence
- platform diagnostics:
  - `codex status` -> exit 1, `stdin is not a terminal`（platform_na）
  - `codex --version` -> `codex-cli 0.118.0`
  - `codex --help` -> ok
- gate order:
  1. `dotnet build ClassroomToolkit.sln -c Debug` -> PASS
  2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` -> FAIL（现存失败，见下）
  3. `dotnet test ... --filter "...ArchitectureDependencyTests|...InteropHookLifecycleContractTests|...InteropHookEventDispatchContractTests|...GlobalHookServiceLifecycleContractTests|...CrossPageDisplayLifecycleContractTests"` -> PASS
  4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1` -> PASS
- focused regression:
  - `dotnet test ... --filter "FullyQualifiedName~CrossPageDisplayUpdate|FullyQualifiedName~PhotoPanInteractiveRefreshPolicyTests|FullyQualifiedName~PhotoInkPanRedrawPolicyTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"` -> PASS (49/49)

## Blocking Gate (existing)
- full `dotnet test` 唯一失败：
  - `RunLocalQualityGatesProfilePropagationContractTests.StableTestsStep_ShouldPassThrough_SelectedProfile`
  - 失败原因：`scripts/quality/run-local-quality-gates.ps1` 未包含 `-Profile $Profile` 透传。
- 该文件在本次改动前已处于工作区变更（`git status` 显示 modified），不属于本次性能修复写入范围。

## N/A Records
- type: platform_na
- reason: `codex status` 在非交互终端不可用（`stdin is not a terminal`）
- alternative_verification: 使用 `codex --version`、`codex --help` 与仓库门禁命令链补证
- evidence_link: 本文件 + 命令输出
- expires_at: 2026-05-03

## Rollback
- 代码回滚入口：`git checkout --` 指定本次改动文件（或按提交回滚）。
- 功能回滚优先级：
  1. 回退阈值文件（`CrossPageDisplayUpdateMinIntervalThresholds.cs`, `PhotoPanInteractiveRefreshPolicy.cs`, `InkRuntimeTimingDefaults.cs`）
  2. 回退 sidecar stale-key 校验段
  3. 回退 `ArrayPool<byte>` 位图复制实现
- 回滚后复验：重复 `build -> test -> contract/invariant -> hotspot`。

## Continuous Execution Update (2026-04-03)
- 修复 `scripts/quality/run-local-quality-gates.ps1` 的 profile 透传：`-Profile quick -> -Profile $Profile`。
- 复验结果：
  - `dotnet build ClassroomToolkit.sln -c Debug` -> PASS
  - `dotnet build-server shutdown; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --disable-build-servers` -> PASS (3117/3117)
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"` -> PASS
  - `powershell -File scripts/quality/check-hotspot-line-budgets.ps1` -> PASS
- 说明：曾出现 WPF 编译缓存文件被占用导致的瞬时失败；通过 build-server shutdown + no-build 测试策略已稳定消除。
