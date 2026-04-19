# 2026-04-19 Image Manager Click Behavior

- rule_ids: `R1`, `R2`, `R6`, `R8`
- risk_level: `low`
- boundary: `src/ClassroomToolkit.App/Photos`
- current_landing: `ImageManagerWindow` item activation flow
- target_destination: normal mode single-click only selects; double-click opens folders or PDF/image fullscreen

## Basis

- User report: in `资源管理` normal mode, all visible items should follow `single-click = select`, `double-click = open`.
- Existing behavior: right-pane activation still opened previewable files on single-click, so selection and activation were coupled.

## Changes

- Added `ImageManagerActivationPolicy` to isolate what may open on single vs double click.
- Updated `OnImageListPointerUp` to stop activation for all visible items and leave only selection semantics.
- Added `OnImageListMouseDoubleClick` so double-click opens folders and previewable files (`PDF` / image).
- Added regression tests covering single-click and double-click activation matrix.

## Commands

1. `codex --version`
2. `codex --help`
3. `codex status`
4. `dotnet build ClassroomToolkit.sln -c Debug --artifacts-path D:\OneDrive\CODE\ClassroomToolkit\.agent-build\artifacts`
5. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ImageManagerActivationPolicyTests" --artifacts-path D:\OneDrive\CODE\ClassroomToolkit\.agent-build\artifacts`
6. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --artifacts-path D:\OneDrive\CODE\ClassroomToolkit\.agent-build\artifacts`
7. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests" --artifacts-path D:\OneDrive\CODE\ClassroomToolkit\.agent-build\artifacts`

## Key Output

- `codex --version`: `codex-cli 0.121.0`
- `codex --help`: succeeded
- `codex status`: failed with `stdin is not a terminal`
- `build`: passed, `0 Warning(s)`, `0 Error(s)`
- targeted regression test: passed, `14` tests
- full test: passed, `3303` tests
- contract/invariant: passed, `28` tests

## Platform N/A

- type: `platform_na`
- reason: `codex status` cannot run in this non-interactive shell and returns `stdin is not a terminal`
- alternative_verification: used `codex --version` + `codex --help`, and recorded active rule path from repository `AGENTS.md`
- evidence_link: `docs/change-evidence/20260419-image-manager-click-behavior.md`
- expires_at: `2026-05-19`

## Gate Notes

- To avoid locked default `bin/obj` outputs from running local app instances, verification used `--artifacts-path`.
- Full test initially missed `brush-dpi-golden.json` because the golden test resolves baseline relative to `AppContext.BaseDirectory`; mirrored existing baseline to `.agent-build/Baselines/brush-dpi-golden.json` before rerun.

## Hotspot Review

- Reviewed [ImageManagerWindow.Navigation.cs](D:\OneDrive\CODE\ClassroomToolkit\src\ClassroomToolkit.App\Photos\ImageManagerWindow.Navigation.cs) activation branch:
  single-click now exits early for all items; double-click dispatches folder navigation or preview open.
- Reviewed [ImageManagerWindow.xaml](D:\OneDrive\CODE\ClassroomToolkit\src\ClassroomToolkit.App\Photos\ImageManagerWindow.xaml):
  both thumbnail and list views route double-click into the same folder-open handler.
- Reviewed [ImageManagerActivationPolicy.cs](D:\OneDrive\CODE\ClassroomToolkit\src\ClassroomToolkit.App\Photos\ImageManagerActivationPolicy.cs):
  policy matrix is minimal and matches tests.
- Conclusion: no additional regression risk found in multi-select path, preview path, or folder navigation path.

## Rollback

1. Revert `src/ClassroomToolkit.App/Photos/ImageManagerActivationPolicy.cs`
2. Revert the handler changes in `src/ClassroomToolkit.App/Photos/ImageManagerWindow.Navigation.cs`
3. Revert the `MouseDoubleClick` wiring in `src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml`
4. Revert `tests/ClassroomToolkit.Tests/ImageManagerActivationPolicyTests.cs`
