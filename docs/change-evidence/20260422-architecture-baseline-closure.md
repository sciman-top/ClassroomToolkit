# 20260422 Architecture Baseline Closure

## Goal

Close architecture baseline debt by removing allow-list based guards, enforcing strict boundary checks, and eliminating remaining App-to-Infra runtime bridge coupling.

## Scope

- `tests/ClassroomToolkit.Tests/ArchitectureDependencyTests.cs`
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Layout.cs`
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Presentation.cs`
- `src/ClassroomToolkit.App/Paint/WindowScreenBoundsResolver.cs`
- `src/ClassroomToolkit.App/Windowing/PresentationForegroundSuppressionInteropAdapter.cs`
- `src/ClassroomToolkit.App/Windowing/WindowStyleIndexPolicy.cs`
- `src/ClassroomToolkit.App/Ink/InkHistoryPersistenceBridge.cs`
- `src/ClassroomToolkit.Application/Abstractions/IInkHistoryStoreBridge.cs` (new)
- `src/ClassroomToolkit.Application/Abstractions/InkHistoryLoadResult.cs` (new)
- `src/ClassroomToolkit.Infra/Storage/InkHistorySqliteStoreAdapter.cs`
- `src/ClassroomToolkit.Infra/Storage/IInkHistoryStoreBridge.cs` (deleted)
- `src/ClassroomToolkit.Infra/Storage/InkHistoryLoadResult.cs` (deleted)
- `tests/ClassroomToolkit.Tests/App/PaintOverlayForegroundProcessContractTests.cs`
- `tests/ClassroomToolkit.Tests/InkHistorySqliteStoreAdapterTests.cs`

## Changes

1. Removed `ArchitectureDependencyTests` baseline allow-lists and switched to hard boundary rules:
   - `ClassroomToolkit.Infra` usage in App layer is only allowed in `App.xaml.cs` (composition root).
   - `ClassroomToolkit.Interop` / `Interop.NativeMethods` / `NativeMethods.` usage in App layer is only allowed under `src/ClassroomToolkit.App/Windowing/`.
2. Completed App-side Interop收口 (non-Windowing access removed):
   - replaced direct style index constant with `WindowStyleIndexPolicy.ExStyle`.
   - replaced direct foreground/native window checks with `PresentationResolver` and Windowing adapters.
3. Eliminated App `Ink` module direct dependency on `ClassroomToolkit.Infra.Storage.*`:
   - moved `IInkHistoryStoreBridge` and `InkHistoryLoadResult` into `ClassroomToolkit.Application.Abstractions`.
   - updated App bridge and Infra adapter to use Application abstractions.
4. Updated affected contract/unit tests to align with new architecture boundaries.

## Verification

1. Codex minimum diagnostics:
   - `codex --version` -> `codex-cli 0.122.0`
   - `codex --help` -> `PASS`
   - `codex status` -> `platform_na` (`stdin is not a terminal`)
2. Build gate:
   - `dotnet build ClassroomToolkit.sln -c Debug`
   - result: `PASS` (`0 errors`, `0 warnings`)
3. Test gate:
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
   - result: `PASS` (`3425 passed`)
4. Contract/invariant gate:
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - result: `PASS` (`28 passed`)
5. Hotspot + full quick quality chain:
   - `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile quick -Configuration Debug`
   - result: `ALL PASS` (includes `hotspot`, `governance-truth-source`, `dependency-governance`)

## N/A Records

- `platform_na`
  - `reason`: `codex status` requires interactive terminal in current execution environment.
  - `alternative_verification`: `codex --version` + `codex --help`.
  - `evidence_link`: `docs/change-evidence/20260422-architecture-baseline-closure.md`
  - `expires_at`: `n/a`

## Risks

- `ArchitectureDependencyTests` now blocks all non-composition-root App->Infra references; future wiring changes must stay in composition root or be abstracted upward.
- Ink history bridge contract relocation changes public namespace ownership from `Infra` to `Application`; any external scripts/tests that hard-code old namespace must be updated.

## Rollback

Revert this change set:

- `git restore --source=HEAD~1 -- tests/ClassroomToolkit.Tests/ArchitectureDependencyTests.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Layout.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Presentation.cs src/ClassroomToolkit.App/Paint/WindowScreenBoundsResolver.cs src/ClassroomToolkit.App/Windowing/PresentationForegroundSuppressionInteropAdapter.cs src/ClassroomToolkit.App/Windowing/WindowStyleIndexPolicy.cs src/ClassroomToolkit.App/Ink/InkHistoryPersistenceBridge.cs src/ClassroomToolkit.Application/Abstractions/IInkHistoryStoreBridge.cs src/ClassroomToolkit.Application/Abstractions/InkHistoryLoadResult.cs src/ClassroomToolkit.Infra/Storage/InkHistorySqliteStoreAdapter.cs tests/ClassroomToolkit.Tests/App/PaintOverlayForegroundProcessContractTests.cs tests/ClassroomToolkit.Tests/InkHistorySqliteStoreAdapterTests.cs docs/change-evidence/20260422-architecture-baseline-closure.md`
- `git restore --source=HEAD~1 --staged --worktree src/ClassroomToolkit.Infra/Storage/IInkHistoryStoreBridge.cs src/ClassroomToolkit.Infra/Storage/InkHistoryLoadResult.cs`
