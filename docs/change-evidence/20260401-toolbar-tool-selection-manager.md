# 20260401 toolbar-tool-selection-manager

- rule_id: R1/R2/R6/R8
- risk_level: low
- scope_boundary:
  - current: `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml(.cs)`
  - target: `src/ClassroomToolkit.App/Paint/PaintToolSelectionManager.cs`
  - batch: `toolbar-tool-selection-v1`

## Basis
- 工具条模式切换原逻辑分散在 `Checked/Unchecked`，二次点击与回退行为不一致。
- 目标：统一为状态机，支持“二次点击取消并回退最近功能工具”，并确保 `Cursor` 不进入历史。

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
- evidence_link: `docs/change-evidence/20260401-toolbar-tool-selection-manager.md`
- expires_at: `2026-04-08`

## Rollback
1. `git restore src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml`
2. `git restore src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`
3. `git restore src/ClassroomToolkit.App/Paint/PaintToolSelectionManager.cs`
4. `git restore tests/ClassroomToolkit.Tests/PaintToolSelectionManagerTests.cs`
