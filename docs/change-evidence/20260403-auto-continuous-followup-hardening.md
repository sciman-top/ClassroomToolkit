# 2026-04-03 Auto Continuous Follow-up Hardening

- Rule IDs: `R1 R2 R4 R6 R8`
- Risk Level: `medium`
- Scope:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.cs`
  - `tests/ClassroomToolkit.Tests/PaintOverlayWhiteboardPhotoCacheContractTests.cs`
  - `scripts/quality/run-local-quality-gates.ps1`

## Basis

- Whiteboard/photo session switch may leave `_boardSuspendedPhotoCache` stale if photo mode exits while whiteboard is active.
- Quality-gate profile propagation contract failed because `run-local-quality-gates.ps1` hardcoded `stable-tests` to `-Profile quick`.

## Changes

- In `ExitPhotoMode()`, explicitly reset `_boardSuspendedPhotoCache = false`.
- Add contract test `PaintOverlayWhiteboardPhotoCacheContractTests` to lock the reset behavior.
- Change quality gate script stable-tests step to pass through selected profile:
  - from `-Profile quick`
  - to `-Profile $Profile`

## Commands

- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## Evidence

- Build: pass (`0 errors`)
- Tests: pass (`3139 passed`)
- Contract/invariant subset: pass (`25 passed`)
- Hotspot: pass (`status=PASS`)
- Note: one transient parallel-run file-lock (`MarkupCompile.cache`) occurred and was resolved by sequential rerun.

## Rollback

- Revert:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.cs`
  - `tests/ClassroomToolkit.Tests/PaintOverlayWhiteboardPhotoCacheContractTests.cs`
  - `scripts/quality/run-local-quality-gates.ps1`


# Backfill 2026-04-03
规则ID=BACKFILL-LEGACY-EVIDENCE-2026-04-03
影响模块=legacy-governance-evidence
当前落点=E:/CODE/ClassroomToolkit/docs/change-evidence
目标归宿=E:/CODE/governance-kit/source/project/ClassroomToolkit/*
迁移批次=2026-04-03-evidence-backfill
风险等级=Low(documentation backfill only)
执行命令=backfill-evidence-template-fields.ps1
验证证据=template-field-backfill-2026-04-03
回滚动作=git revert evidence backfill commit
