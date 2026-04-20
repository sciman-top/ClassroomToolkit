# 2026-04-20 Photo Overlay Failure Branch Validation

- rule_ids: `R1`, `R2`, `R6`, `R8`
- risk_level: `low`
- boundary: `tests/ClassroomToolkit.Tests` contract-only validation
- current_landing: `main` with clean workspace before change
- target_destination: add minimal verification for `PhotoOverlayWindow` null-bitmap failure branch without changing runtime behavior

## Basis

- User instruction: execute prior recommendation with evidence-first approach.
- Prior conclusion: `bitmap == null` branch lacked explicit coverage; behavior should be validated before considering code change.
- Scope decision: add source contract tests only; keep application code unchanged for compatibility.

## Commands

1. `codex --version`
2. `codex --help`
3. `codex status`
4. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoOverlayLoadFailureBranchContractTests"`
5. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoOverlay|FullyQualifiedName~RollCallWindowPhotoOverlayReuseContractTests|FullyQualifiedName~UiCopyContractTests|FullyQualifiedName~PhotoOverlayWindowXamlLayoutContractTests|FullyQualifiedName~OverlayWindowsXamlContractTests"`
6. `dotnet build ClassroomToolkit.sln -c Debug`
7. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
8. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`

## Key Output

- `codex --version`: `codex-cli 0.121.0`
- `codex --help`: succeeded
- targeted new validation tests: `2 passed`
- overlay/rollcall related regression subset: `41 passed`
- build gate: passed (`0` warnings, `0` errors)
- full test gate: `3329 passed`
- contract/invariant gate: `28 passed`

## Platform N/A

- type: `platform_na`
- reason: `codex status` cannot run in non-interactive shell (`stdin is not a terminal`)
- alternative_verification: `codex --version`, `codex --help`, and repository/test gate commands completed
- evidence_link: `docs/change-evidence/20260420-photo-overlay-failure-branch-validation.md`
- expires_at: `2026-05-20`

## Hotspot Review

- file: `tests/ClassroomToolkit.Tests/PhotoOverlayLoadFailureBranchContractTests.cs`
- checks:
  - validates `ApplyLoadedBitmap` null-bitmap branch stops timer and conditionally hides.
  - validates branch does not call immediate cache-clear/callback path.
  - validates `apply-null` telemetry token remains present.
- conclusion: validation-only change; no runtime path or external behavior modified.

## Rollback

1. Remove file `tests/ClassroomToolkit.Tests/PhotoOverlayLoadFailureBranchContractTests.cs`.
2. Re-run gate sequence to confirm rollback cleanliness.
