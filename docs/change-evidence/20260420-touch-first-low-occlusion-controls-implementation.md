# Touch-first low-occlusion controls implementation evidence

Date: 2026-04-20

## Scope
- Rule IDs: R1, R2, R4, R6, R7, R8; E4/E6 N/A for this UI-only change.
- Boundary: touch-first usability for launcher, toolbar, image manager, photo/PDF browsing, timer/auto-exit/photo overlay.
- Current landing: code and tests in working tree.
- Target home: implementation plan `docs/superpowers/plans/2026-04-20-touch-first-low-occlusion-controls.md`.

## Changes
- Added shared touch target tokens and touch-aware long-press support.
- Added repeat-tap local settings for quick colors and shapes.
- Replaced hidden board double-click/long-press dependency with compact explicit board actions.
- Added launcher overflow menu and touch handlers for the launcher bubble while keeping the visual bubble compact.
- Made image manager single-tap open folders/images/PDF and added explicit multi-select entry.
- Made photo/PDF touch browsing finger-first while preserving two-finger zoom.
- Added touch repeat handlers to timer steppers, removed forced auto-exit keyboard focus, and made photo overlay close explicit.
- Split toolbar touch-first handlers into `PaintToolbarWindow.TouchFirstActions.cs` to satisfy hotspot line budget.

## Commands And Evidence

### build
Command:
```powershell
dotnet build ClassroomToolkit.sln -c Debug
```
Result: PASS
Key output: `0 warning / 0 error`

### test
Command:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
```
Result: PASS
Key output: `3383 passed, 0 failed, 0 skipped`

### contract/invariant
Command:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
```
Result: PASS
Key output: `28 passed, 0 failed, 0 skipped`

### hotspot
Command:
```powershell
powershell -File scripts/quality/check-hotspot-line-budgets.ps1
```
Result: PASS
Key output: `[hotspot] PASS - all .cs files within line budget (max=1200)`

## Codex Platform Diagnostics

Command:
```powershell
codex --version
```
Exit code: 0
Key output: `codex-cli 0.121.0`

Command:
```powershell
codex --help
```
Exit code: 0
Key output: help text rendered, commands include `exec`, `review`, `mcp`, `sandbox`, `resume`, `cloud`, `help`.

Command:
```powershell
codex status
```
Exit code: 1
Classification: `platform_na`
reason: non-interactive terminal in this execution context.
alternative_verification: `codex --version` and `codex --help` completed successfully; active repo rules supplied in task context.
evidence_link: this file.
expires_at: 2026-04-27
Key output: `Error: stdin is not a terminal`

## Hotspot Review
- `PaintToolbarWindow.xaml.cs` was over line budget after toolbar changes.
- Remediation: moved touch-first quick color/shape/board handlers into `PaintToolbarWindow.TouchFirstActions.cs`.
- Verification: hotspot script passed after split.

## Rollback
- Revert working tree changes for this implementation using git checkout/restore for the files listed by `git status --short` if rollback is required.
- If committed later, rollback via `git revert <commit>` for the implementation commit.
- No data format changes were made to `students.xlsx`, `student_photos/`, or `settings.ini`.
