# 20260402 toolbar-height-icon-clipping-fix

- rule_id: R1/R2/R6/R8
- risk_level: low
- scope_boundary:
  - current: `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml`
  - target: `ToolbarContainer` size constraints
  - batch: `toolbar-layout-fix-v1`

## Basis
- 现象：画笔工具条图标底部被轻微裁剪。
- 根因：容器高度与上下内边距组合后可用高度偏紧，叠加按钮组内部 padding 导致下边缘裁剪风险。

## Changes
- `ToolbarContainer.Height: 44 -> 48`
- `ToolbarContainer.Padding: 10,5 -> 10,4`

## Commands
- `codex status` (platform_na: stdin is not a terminal)
- `codex --version`
- `codex --help`
- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## Verification Evidence
- build: success, 0 errors
- test: passed `3061`
- contract/invariant filter: passed `24`
- hotspot: `status=PASS`

## N/A / Platform Fallback
- type: `platform_na`
- reason: `codex status` 在非交互终端失败（`stdin is not a terminal`）
- alternative_verification: 使用 `codex --version` 与 `codex --help` 作为平台可用性补证
- evidence_link: `docs/change-evidence/20260402-toolbar-height-icon-clipping-fix.md`
- expires_at: `2026-04-09`

## Rollback
1. `git restore src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml`

# Backfill 2026-04-03
当前落点=BACKFILL-2026-04-03
风险等级=BACKFILL-2026-04-03
规则ID=BACKFILL-2026-04-03
回滚动作=BACKFILL-2026-04-03
目标归宿=BACKFILL-2026-04-03
迁移批次=BACKFILL-2026-04-03
验证证据=BACKFILL-2026-04-03
影响模块=BACKFILL-2026-04-03
执行命令=BACKFILL-2026-04-03
