# Terminal Blueprint Cutover Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finish the remaining terminal-blueprint work so ClassroomToolkit reaches its declared end-state architecture without changing the chosen stack or regressing classroom-critical behavior.

**Architecture:** Keep the existing `WPF + .NET 10` modular monolith and continue the current extraction path: hotspot window files become thin view/coordinator shells, `Session / Policy / Updater / Executor` own runtime decisions, `Windowing` owns z-order/topmost/focus/owner behavior, `Infra` owns `JSON + SQLite`, and `Interop` remains a shielded boundary with shrinking App-layer exposure.

**Tech Stack:** .NET 10, WPF, xUnit, FluentAssertions, PowerShell validation scripts, SQLite, existing ClassroomToolkit architecture guards and manual classroom regression matrix.

---

## Preconditions And Source Of Truth

- Authoritative architecture target:
  - `E:\PythonProject\ClassroomToolkit\docs\plans\2026-03-06-best-target-architecture-plan.md`
  - `E:\PythonProject\ClassroomToolkit\docs\architecture\2026-03-10-target-boundary-map.md`
  - `E:\PythonProject\ClassroomToolkit\docs\architecture\2026-03-10-interop-direct-dependency-matrix.md`
  - `E:\PythonProject\ClassroomToolkit\docs\handover.md`
- This plan assumes the project keeps:
  - `WPF` as the primary UI shell
  - `Session / Policy / Updater / Executor` as the runtime extraction direction
  - `JSON` as default config path and `SQLite` as the business-data target path
  - `CPU-first, GPU-optional` Ink runtime policy
- Do not migrate to `WinUI 3`, do not expand `App -> Interop`, and do not reintroduce workbook/INI as runtime primary storage.
- Before each chunk, inspect `git status` and verify no unrelated local changes need isolation.

## File Structure

### Existing files that remain authoritative during this cutover

- `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\MainWindow.xaml.cs`
- `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\MainWindow.Paint.cs`
- `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\MainWindow.Photo.cs`
- `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Paint\PaintOverlayWindow.xaml.cs`
- `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Paint\PaintOverlayWindow.Input.cs`
- `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Paint\PaintOverlayWindow.Presentation.cs`
- `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\RollCallWindow.xaml.cs`
- `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\RollCallWindow.Windowing.cs`
- `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\RollCallWindow.Input.cs`
- `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Session\SessionCoordinator.cs`
- `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Windowing\WindowOrchestrator.cs`
- `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Windowing\NativeCursorWindowGeometryInteropAdapter.cs`
- `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Windowing\NativeWindowPlacementInteropAdapter.cs`
- `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Windowing\NativeWindowStyleInteropAdapter.cs`
- `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Windowing\NativeWindowTopmostInteropAdapter.cs`
- `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Windowing\PresentationForegroundSuppressionInteropAdapter.cs`
- `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Windowing\WindowHandleValidationInteropAdapter.cs`
- `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.Infra\` (existing config/store/resolver files)
- `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Ink\`
- `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Paint\Brushes\`
- `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\ArchitectureDependencyTests.cs`

### New files expected during implementation

- Additional focused `Policy`, `StateUpdater`, `Executor`, or `Coordinator` files under:
  - `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Windowing\`
  - `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Session\`
  - `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Paint\`
  - `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.Application\UseCases\`
  - `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.Infra\`
- Matching tests under:
  - `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\`
  - `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\`
  - `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\Session\`
- Freeze/acceptance updates under:
  - `E:\PythonProject\ClassroomToolkit\docs\validation\`
  - `E:\PythonProject\ClassroomToolkit\docs\handover.md`
  - `E:\PythonProject\ClassroomToolkit\docs\plans\2026-03-06-best-target-architecture-plan.md`
  - `E:\PythonProject\ClassroomToolkit\docs\architecture\2026-03-10-target-boundary-map.md`
  - `E:\PythonProject\ClassroomToolkit\docs\architecture\2026-03-10-interop-direct-dependency-matrix.md`

## Chunk 1: Main Window And Floating Coordination Tail

### Task 1: Empty the remaining orchestration debt from `MainWindow.*`

**Files:**
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\MainWindow.xaml.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\MainWindow.Paint.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\MainWindow.Photo.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Session\SessionCoordinator.cs`
- Create: focused extraction files under `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Windowing\` and `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Session\`
- Test: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\MainWindowOverlayInteractionStatePolicyTests.cs`
- Test: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\MainWindowOnClosingPlanPolicyTests.cs`
- Test: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\FloatingWindowCoordinatorTests.cs`
- Test: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\WindowOrchestratorTests.cs`

- [ ] Step 1: Inventory every remaining direct runtime write, z-order decision, focus decision, and owner/topmost retouch branch in `MainWindow.*`; classify each as `Session`, `Windowing`, or `Application` responsibility.
- [ ] Step 2: For each branch family, add or extend a failing focused test before moving code.
- [ ] Step 3: Extract one responsibility at a time into a named `Policy`, `StateUpdater`, `Executor`, or `Coordinator`; keep `MainWindow.*` as a wiring shell.
- [ ] Step 4: Remove duplicate condition trees from `MainWindow.*` once the extracted path is wired and covered.
- [ ] Step 5: Run the targeted test set.

Run:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~MainWindow|FullyQualifiedName~FloatingWindow|FullyQualifiedName~WindowOrchestrator"
```

Expected: PASS with no new `ArchitectureDependencyTests` failures.

### Task 2: Converge floating-window repair and activation through one runtime path

**Files:**
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Windowing\FloatingWindowCoordinator.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Windowing\FloatingTopmostExecutionExecutor.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Windowing\WindowActivationExecutor.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Windowing\SessionTransitionWindowingPolicy.cs`
- Test: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\FloatingTopmostExecutionExecutorTests.cs`
- Test: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\WindowActivationExecutorTests.cs`
- Test: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\SessionTransitionWindowingPolicyTests.cs`

- [ ] Step 1: Write a failing test for one remaining duplicate activation/topmost path.
- [ ] Step 2: Route that path through the existing `FloatingWindowCoordinator`/executor chain instead of ad-hoc window code.
- [ ] Step 3: Tighten diagnostics/reason-policy coverage so runtime logs still explain failures.
- [ ] Step 4: Repeat until the remaining duplicate direct-repair paths in `MainWindow.*` are gone.
- [ ] Step 5: Re-run the floating/windowing suite.

Run:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~FloatingTopmost|FullyQualifiedName~WindowActivation|FullyQualifiedName~SessionTransitionWindowing"
```

Expected: PASS and no behavior drift in window coordination tests.

## Chunk 2: Paint Overlay Terminalization

### Task 3: Finish shrinking `PaintOverlayWindow` into thin shell + extracted runtime units

**Files:**
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Paint\PaintOverlayWindow.xaml.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Paint\PaintOverlayWindow.Input.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Paint\PaintOverlayWindow.Presentation.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Session\PaintOverlaySessionEffectRunner.cs`
- Create: any remaining extracted units under `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Paint\` or `...\Windowing\`
- Test: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\OverlayPresentationCommandRouterTests.cs`
- Test: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\OverlayWheelPresentationExecutionPolicyTests.cs`
- Test: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\PhotoManipulationAdmissionPolicyTests.cs`
- Test: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\CrossPageInputSwitchExecutionPolicyTests.cs`
- Test: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\Session\UiSessionPresentationInputPolicyTests.cs`

- [ ] Step 1: List any remaining `PaintOverlayWindow*` branches that still mix UI events, state mutation, and external command dispatch in one method.
- [ ] Step 2: Add or extend failing tests for those branches at the extracted policy/executor level instead of adding more window-level tests.
- [ ] Step 3: Extract the mixed branches into named units and wire them through `Session`/`Windowing`/Presentation runtime boundaries.
- [ ] Step 4: Delete obsolete wrappers and dead private helpers after coverage passes.
- [ ] Step 5: Re-run the targeted overlay/cross-page/presentation suite.

Run:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~Overlay|FullyQualifiedName~CrossPage|FullyQualifiedName~PhotoManipulation|FullyQualifiedName~Presentation"
```

Expected: PASS, no new App-layer `Interop` usage, and no regression in overlay routing.

### Task 4: Ensure presentation intent flows through `Application -> Services -> Interop`

**Files:**
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Paint\PaintOverlayWindow.Presentation.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.Application\UseCases\` (presentation use-case files)
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.Services\` (presentation runtime gateway files)
- Test: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\PresentationControlServiceTests.cs`
- Test: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\PresentationControlPlannerTests.cs`
- Test: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\PresentationChannelAvailabilityPolicyTests.cs`

- [ ] Step 1: Identify any remaining places where overlay code directly decides external command routing that belongs in `Application`/`Services`.
- [ ] Step 2: Add a failing test around the use-case/service seam.
- [ ] Step 3: Move intent expression upward into `Application`, keep `Services` as runtime gateway, and keep `Interop` behind adapters.
- [ ] Step 4: Verify WPS/Office fallback behavior remains unchanged.
- [ ] Step 5: Run the presentation suite.

Run:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PresentationControl|FullyQualifiedName~PresentationChannel|FullyQualifiedName~PresentationFocus"
```

Expected: PASS with unchanged fallback semantics.

## Chunk 3: RollCall And Remaining App-Side Runtime Debt

### Task 5: Bring `RollCallWindow` to the same shell-only standard

**Files:**
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\RollCallWindow.xaml.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\RollCallWindow.Windowing.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\RollCallWindow.Input.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Windowing\RollCallVisibilityTransitionPolicy.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Windowing\RollCallTransparencyPolicy.cs`
- Test: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\RollCallWindowDiagnosticsPolicyTests.cs`
- Test: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\RollCallWindowXamlContractTests.cs`
- Test: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\RollCallWorkbookUseCaseTests.cs`

- [ ] Step 1: Inventory remaining direct input/windowing/runtime mutations inside `RollCallWindow*`.
- [ ] Step 2: Add failing tests around the extracted policy boundary for the highest-risk branch.
- [ ] Step 3: Move that branch into `Windowing`, `Session`, or `Application.UseCases.RollCall` as appropriate.
- [ ] Step 4: Repeat until `RollCallWindow*` keeps only view hookup, user feedback, and minimal lifecycle glue.
- [ ] Step 5: Run RollCall-targeted tests plus architecture guards.

Run:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~RollCall|FullyQualifiedName~ArchitectureDependencyTests"
```

Expected: PASS and no new `Interop` expansion.

### Task 6: Shrink the App-layer `Interop` allowlist or explicitly freeze the remainder

**Files:**
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Windowing\NativeCursorWindowGeometryInteropAdapter.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Windowing\NativeWindowPlacementInteropAdapter.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Windowing\NativeWindowStyleInteropAdapter.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Windowing\NativeWindowTopmostInteropAdapter.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Windowing\PresentationForegroundSuppressionInteropAdapter.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Windowing\WindowHandleValidationInteropAdapter.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\ArchitectureDependencyTests.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\docs\architecture\2026-03-10-interop-direct-dependency-matrix.md`

- [ ] Step 1: Audit each allowlisted file and decide whether it is true terminal-boundary code or still hiding non-boundary logic.
- [ ] Step 2: Where boundary leakage exists, extract the non-interop logic out and cover it with a unit test.
- [ ] Step 3: If a file becomes pure adapter code and remains legitimately allowlisted, document why it must stay.
- [ ] Step 4: If any file can leave the allowlist, tighten `ArchitectureDependencyTests` and update the dependency matrix in the same change.
- [ ] Step 5: Run the architecture guard suite.

Run:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~WindowTopmost|FullyQualifiedName~WindowPlacement|FullyQualifiedName~WindowStyle"
```

Expected: PASS with equal-or-smaller allowlist.

## Chunk 4: Storage And Ink End-State Closure

### Task 7: Finish the `JSON + SQLite` runtime cutover for remaining business data

**Files:**
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.Infra\` (remaining workbook/sqlite/config resolver files)
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.Application\UseCases\RollCall\RollCallWorkbookUseCase.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.Services\` and `...\Infra\` resolver/adapter files referenced by the current workbook and ink persistence path
- Test: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\RollCallWorkbookStoreResolverTests.cs`
- Test: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\StudentWorkbookSqliteStoreAdapterTests.cs`
- Test: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\InkHistorySqliteStoreAdapterTests.cs`
- Test: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\InkHistoryPersistenceBridgeTests.cs`

- [ ] Step 1: Produce a short inventory of any runtime writes still landing in workbook/legacy config paths.
- [ ] Step 2: Add or tighten failing tests for the remaining resolver/adapter branch.
- [ ] Step 3: Move the runtime path to `SQLite` or `JSON`, preserving migration, rollback, and compatibility readback where required.
- [ ] Step 4: Update guardrail logging so startup makes the active backend explicit.
- [ ] Step 5: Run the persistence suite.

Run:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~Workbook|FullyQualifiedName~Sqlite|FullyQualifiedName~InkHistory|FullyQualifiedName~AppSettings"
```

Expected: PASS with no new workbook-as-primary runtime path.

### Task 8: Seal Ink behind the single-renderer contract

**Files:**
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Paint\Brushes\InkRendererFactoryResolver.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Paint\Brushes\InkRendererBackendSelectionPolicy.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Ink\InkPersistenceService.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Ink\InkStorageService.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Paint\PaintOverlayWindow.Ink*.cs`
- Test: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\CpuInkRendererFactoryTests.cs`
- Test: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\GpuInkRendererFactoryTests.cs`
- Test: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\InkRendererFactoryResolverTests.cs`
- Test: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\InkRendererBackendSelectionPolicyTests.cs`
- Test: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\InkPersistenceServiceTests.cs`

- [ ] Step 1: Audit remaining places where `PaintOverlayWindow.Ink*` can bypass the intended renderer-selection contract.
- [ ] Step 2: Add a failing test around the bypass or missing fallback case.
- [ ] Step 3: Route the path through the renderer factory/selection policy and preserve CPU-first fallback semantics.
- [ ] Step 4: Verify GPU stays optional, double-gated, and non-blocking for release.
- [ ] Step 5: Run the Ink suite.

Run:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~InkRenderer|FullyQualifiedName~InkPersistence|FullyQualifiedName~InkStorage|FullyQualifiedName~GpuInk|FullyQualifiedName~CpuInk"
```

Expected: PASS with CPU path still publishable when GPU is unavailable.

## Chunk 5: Freeze Validation, Manual Regression, And Documentation Lock

### Task 9: Close automated validation gates

**Files:**
- Modify as required by previous chunks
- Test: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj`
- Script: `E:\PythonProject\ClassroomToolkit\scripts\ctoolkit.ps1`

- [ ] Step 1: Run the highest-signal targeted suites after each chunk instead of waiting for the end.
- [ ] Step 2: Once chunks 1-4 are stable, run full Debug tests.
- [ ] Step 3: Run full Release build and Release tests.
- [ ] Step 4: If a failure appears, fix the owning chunk before touching unrelated code.

Run:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
dotnet build ClassroomToolkit.sln -c Release
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Release
powershell -File scripts/ctoolkit.ps1
```

Expected: PASS for Debug and Release gates, or a documented blocker with owner and rollback path.

### Task 10: Complete the manual classroom regression matrix and freeze docs

**Files:**
- Modify: `E:\PythonProject\ClassroomToolkit\docs\handover.md`
- Modify: `E:\PythonProject\ClassroomToolkit\docs\plans\2026-03-06-best-target-architecture-plan.md`
- Modify: `E:\PythonProject\ClassroomToolkit\docs\architecture\2026-03-10-target-boundary-map.md`
- Modify: `E:\PythonProject\ClassroomToolkit\docs\architecture\2026-03-10-interop-direct-dependency-matrix.md`
- Modify: `E:\PythonProject\ClassroomToolkit\docs\validation\2026-03-06-target-architecture-progress.md`
- Modify: `E:\PythonProject\ClassroomToolkit\docs\validation\target-architecture-final-acceptance.md`

- [ ] Step 1: Execute and record manual regression for `PPT/WPS fullscreen`, `image/PDF cross-page`, `whiteboard`, `scene switching`, `launcher/toolbar/roll-call topmost + focus recovery`, and `4K + projector + cross-monitor DPI`.
- [ ] Step 2: Record any remaining non-blocking defects separately from architecture completion status.
- [ ] Step 3: Update all governing docs in one pass so plan, progress, handover, boundary map, allowlist matrix, and final acceptance tell the same story.
- [ ] Step 4: Explicitly state whether the terminal blueprint is now complete, or list the exact residual blocker.

Manual evidence checklist:
- `PPT / WPS` fullscreen navigation and overlay behavior
- `Image / PDF` fullscreen and cross-page ink behavior
- Whiteboard stability
- Scene switching among `PPT/WPS`, image/PDF, and whiteboard
- Launcher / toolbar / roll-call owner, topmost, activation, and focus restore
- `PerMonitorV2` across 4K, projector, and monitor moves

Expected: docs, guards, and manual evidence all align on a single end-state verdict.

## Exit Criteria

This plan is complete only when all of the following are true:

- `MainWindow.*`, `PaintOverlayWindow.*`, and `RollCallWindow.*` are thin shells instead of decision hubs.
- `Session / Policy / Updater / Executor` owns the runtime decision path for classroom-critical behavior.
- App-layer `Interop` exposure is not larger than today and is smaller if further shrinkage is feasible.
- `JSON` and `SQLite` are the real runtime primary paths for config and business data.
- Ink always enters through the single renderer-selection contract; CPU remains the stable publish path.
- Debug and Release automation pass.
- Manual classroom regression is recorded and accepted.
- Governing docs and dependency guards are in sync.

Plan complete and saved to `docs/superpowers/plans/2026-03-14-terminal-blueprint-cutover-implementation-plan.md`. Ready to execute.
