# 2026-04-03 Whiteboard/Photo Resume Alignment

- Rule IDs: `R1 R2 R4 R6 R8`
- Risk Level: `medium`
- Scope:
  - `src/ClassroomToolkit.App/Services/PaintWindowOrchestrator.cs`
  - `tests/ClassroomToolkit.Tests/PaintWindowOrchestratorWhiteboardContractTests.cs`

## Basis

- `OnToolbarWhiteboardToggled(true)` previously called `OverlayWindow.ExitPhotoMode()`.
- That forced photo-mode teardown before whiteboard sessioning, which could break whiteboard-exit resume semantics (expected: resume to photo scene when entering whiteboard from photo context).
- Overlay already owns whiteboard enter/exit scene dispatch and resume decision (`PaintOverlayWindow.Board.cs` + `WhiteboardResumeSceneResolver.cs`), so orchestrator should not force a scene collapse.

## Commands

- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
- `codex status`
- `codex --version`
- `codex --help`

## Evidence

- Build: pass (`0 errors`)
- Tests: pass (`3138 passed`)
- Contract/invariant subset: pass (`25 passed`)
- Hotspot: pass (`status=PASS`)
- Platform diagnostics:
  - `codex status`: `platform_na` (`stdin is not a terminal`, non-interactive terminal)
  - `codex --version`: pass (`codex-cli 0.118.0`)
  - `codex --help`: pass

## N/A Record

- Type: `platform_na`
- Reason: `codex status` requires interactive terminal in this execution environment.
- Alternative verification: `codex --version` + `codex --help` + project hard-gate command evidence.
- Evidence link: `docs/change-evidence/20260403-whiteboard-photo-resume-alignment.md`
- Expires at: `2026-04-10`

## Rollback

- Revert:
  - changes in `src/ClassroomToolkit.App/Services/PaintWindowOrchestrator.cs`
  - `tests/ClassroomToolkit.Tests/PaintWindowOrchestratorWhiteboardContractTests.cs`

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
