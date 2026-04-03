# 2026-04-03 Paint Toolbar Order Shape Color

- rule_id: R1/R2/R6/R8
- risk_level: low
- active_rule_path: `E:/CODE/ClassroomToolkit/AGENTS.md`
- current_landing: `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml`
- target_destination: `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml`
- rollback: restore the pre-change block order and remove `Brush_ShapeTool_Icon` / `Brush_ResourceTool_Icon`

## Changes
- Swapped toolbar order so `资源管理` now appears before `白板`.
- Unified the main `图形` button icon color with all shape menu item icons.
- Changed `资源管理` icon to teaching green so it is visually distinct from drawing tools, while keeping `白板` on violet.

## Commands
- `codex status`
- `codex --version`
- `codex --help`
- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## Evidence
- `codex status`: failed with `stdin is not a terminal`.
- `codex --version`: passed, `codex-cli 0.118.0`.
- `codex --help`: passed.
- `build`: blocked by locked output files in `src/ClassroomToolkit.App/bin/Debug/net10.0-windows`, held by `sciman Classroom Toolkit (34728)` and `Microsoft Visual Studio (14712)`.
- `test`: blocked by locked temporary build output `src/ClassroomToolkit.App/obj/Debug/net10.0-windows/sciman Classroom Toolkit.dll`.
- `contract/invariant`: blocked by the same build-stage lock.
- `hotspot`: passed.
- UI diff verified at `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml` lines 15-17, 19-58, 143-158.

## N/A
### platform_na
- reason: `codex status` requires interactive stdin in current terminal session.
- alternative_verification: `codex --version` and `codex --help` both passed.
- evidence_link: `docs/change-evidence/20260403-paint-toolbar-order-shape-color.md`
- expires_at: `2026-04-10`

### gate_na
- reason: hard-gate build/test/contract commands are blocked by external process locks on the app output directory, not by this XAML change itself.
- alternative_verification: hotspot pass plus direct XAML diff inspection.
- evidence_link: `docs/change-evidence/20260403-paint-toolbar-order-shape-color.md`
- expires_at: `2026-04-04`
- recovery_plan: close the running `sciman Classroom Toolkit` process and Visual Studio build holders, then rerun the hard-gate sequence in order.
