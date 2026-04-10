# PLANS.md

## Objective
- 执行 `2026-04-10-full-governance-v2` 全量治理计划，在不破坏既有契约前提下提升正确性、性能和可维护性。

## Scope
- In scope:
  - 全仓正确性与鲁棒性清扫
  - 性能与响应优化（启动、绘制、切页、导出、内存）
  - 去冗余、去过度设计、结构收敛
  - 可维护性重构与边界硬化
  - 可观测性/供应链安全/发布工程化补齐
- Out of scope:
  - 新业务功能扩张
  - UI 视觉风格重做
  - 跨技术栈迁移

## Current phase
- Phase 4 in progress: large-file decomposition batches for Ink/Overlay are running with full-gate validation after each split

## Steps
1. 先完成基线与风险台账（证据化）
2. 按 `build -> test -> contract/invariant -> hotspot` 推进分阶段治理
3. 每周 checkpoint 复盘并滚动更新证据与回滚方案

## Validation
- build: `dotnet build ClassroomToolkit.sln -c Debug`
- test: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- contract: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- hotspot: `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## Risks
- 热点重构导致行为漂移
- 性能优化引发稳定性回退
- 去冗余误删差异逻辑

## Rollback
- 任一阻断级问题触发立即回滚，按 `docs/runbooks/migration-rollback-playbook.md` 执行并重跑全门禁。
