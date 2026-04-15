# 2026-04-03 Photo WindowMode Switch Hardening

- Rule IDs: `R1 R2 R4 R6 R8`
- Risk Level: `medium`
- Scope:
  - `src/ClassroomToolkit.App/Paint/PhotoWindowStateRestorePolicy.cs`
  - `src/ClassroomToolkit.App/Paint/PhotoWindowModeZOrderRetouchPolicy.cs`
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.cs`
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.cs`
  - `tests/ClassroomToolkit.Tests/PhotoWindowStateRestorePolicyTests.cs`
  - `tests/ClassroomToolkit.Tests/PhotoWindowModeZOrderRetouchPolicyTests.cs`

## Basis

- Photo mode minimize/restore previously forced fullscreen restore regardless of pre-minimize mode.
- Photo-mode internal fullscreen/windowed switches did not proactively request floating z-order retouch.

## Commands

- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## Evidence

- Build: pass (`0 errors`)
- Tests: pass (`3137 passed`)
- Contract/invariant subset: pass (`25 passed`)
- Hotspot: pass (`status=PASS`)

## Rollback

- Revert:
  - `src/ClassroomToolkit.App/Paint/PhotoWindowStateRestorePolicy.cs`
  - `src/ClassroomToolkit.App/Paint/PhotoWindowModeZOrderRetouchPolicy.cs`
  - changes in:
    - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.cs`
    - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.cs`
    - `tests/ClassroomToolkit.Tests/PhotoWindowStateRestorePolicyTests.cs`
    - `tests/ClassroomToolkit.Tests/PhotoWindowModeZOrderRetouchPolicyTests.cs`

# Backfill 2026-04-03
规则ID=BACKFILL-LEGACY-EVIDENCE-2026-04-03
影响模块=legacy-governance-evidence
当前落点=D:/OneDrive/CODE/ClassroomToolkit/docs/change-evidence
目标归宿=D:/OneDrive/CODE/repo-governance-hub/source/project/ClassroomToolkit/*
迁移批次=2026-04-03-evidence-backfill
风险等级=Low(documentation backfill only)
执行命令=backfill-evidence-template-fields.ps1
验证证据=template-field-backfill-2026-04-03
回滚动作=git revert evidence backfill commit
