# Presentation Navigation Stability Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** eliminate frequent regressions in PPT/WPS fullscreen page navigation (brush/cursor, keyboard/wheel) by unifying decision flow and debounce ownership behind one orchestrator.

**Architecture:** keep UI window as thin event ingress only; move navigation intent parsing, context snapshot, routing decision, and debounce into dedicated policies/orchestrator with deterministic tests. Preserve current external behavior first, then optimize latency on top of stable contracts.

**Tech Stack:** .NET 10, WPF Dispatcher, existing `ClassroomToolkit.App.Paint` policy pattern, xUnit + FluentAssertions contract/matrix tests.

---

## 1. Requirement Boundary (终态方案先行)

Core in-scope:
- PPT/WPS fullscreen slideshow navigation consistency.
- Input sources: keyboard + wheel.
- Tool modes: brush + cursor.
- Route channels: hook path + overlay key path + direct send path.
- Regression-proof test matrix and contract gates.

Non-functional targets:
- Keyboard page-turn perceived latency: no extra queueing delay relative to current fixed build.
- Deterministic decision: same context snapshot must produce same decision.
- Safety: no behavior change for photo/board mode and non-slideshow windows.

Dependencies:
- `PaintOverlayWindow.Presentation.cs`
- `WpsHook*Policy` and `Presentation*Policy`
- `PresentationControlService` debounce path
- existing test gate: `WpsHook*`, `Presentation*`, contract tests.

Out-of-scope:
- COM/WPS interop protocol redesign.
- New global hook type.
- UI redesign or settings panel redesign.

## 2. Target Architecture (目标归宿)

Current hotspot:
- `PaintOverlayWindow.Presentation.cs` holds event handling + context resolve + routing + debounce + dispatch priority.

Target modules:
- `PresentationNavigationIntentParser`: normalize source events into one intent model.
- `PresentationNavigationContextSnapshotBuilder`: gather read-only runtime context.
- `PresentationNavigationOrchestrator`: single decision entry.
- `PresentationNavigationDebounceCoordinator`: single debounce owner for page-nav suppression.
- `PresentationNavigationDispatchPriorityPolicy`: source-based priority only.

Dependency direction:
- `PaintOverlayWindow` -> Orchestrator -> Policies/Coordinator -> existing send adapters.
- UI code cannot contain new branching rules except parameter collection and fallback dispatch.

Data/state ownership:
- Snapshot is immutable per event.
- Debounce state owned by coordinator, not duplicated in window and service layers.

## 3. Migration Strategy (分阶段迁移 + 回滚)

Phase A (behavior freeze):
- Introduce models/orchestrator with parity behavior.
- Keep old path available behind temporary toggle inside window method scope.
- Rollback point: remove orchestrator call sites, restore old inline decision.

Phase B (single debounce ownership):
- Move suppression checks to coordinator.
- Remove duplicated suppression branches from window/service where overlapping.
- Rollback point: restore old suppression methods and call sites.

Phase C (matrix hardening + latency tune):
- Add matrix tests covering PPT/WPS x brush/cursor x keyboard/wheel x foreground/background.
- Tune dispatch priority only after parity tests pass.
- Rollback point: revert priority policy file and matrix-specific behavior tweaks.

## 4. Verification Gates

Mandatory commands per phase:
- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~WpsHook|FullyQualifiedName~Presentation|FullyQualifiedName~Overlay"`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PresentationNavigationRegressionMatrixTests"`

Manual acceptance:
- WPS fullscreen: brush/cursor + keyboard/wheel.
- PPT fullscreen: brush/cursor + keyboard/wheel.
- Mode switch path: brush -> cursor repeated 20+ turns.

## 5. Task List (可执行清单)

### Task 1: Introduce Unified Navigation Models

**Files:**
- Create: `src/ClassroomToolkit.App/Paint/PresentationNavigationIntent.cs`
- Create: `src/ClassroomToolkit.App/Paint/PresentationNavigationContextSnapshot.cs`
- Create: `src/ClassroomToolkit.App/Paint/PresentationNavigationDecision.cs`
- Test: `tests/ClassroomToolkit.Tests/PresentationNavigationModelsTests.cs`

- [x] **Step 1: Write failing tests for model invariants**
- [x] **Step 2: Run test to verify fail**
Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PresentationNavigationModelsTests"`
Expected: FAIL for missing types or mismatch defaults.
- [x] **Step 3: Implement minimal models**
- [x] **Step 4: Re-run tests**
Expected: PASS.
- [ ] **Step 5: Commit**
`git commit -m "refactor: add presentation navigation unified models"`

### Task 2: Extract Orchestrator with Behavior Parity

**Files:**
- Create: `src/ClassroomToolkit.App/Paint/PresentationNavigationOrchestrator.cs`
- Create: `src/ClassroomToolkit.App/Paint/PresentationNavigationIntentParser.cs`
- Modify: `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Presentation.cs`
- Test: `tests/ClassroomToolkit.Tests/PresentationNavigationOrchestratorTests.cs`

- [x] **Step 1: Write failing orchestrator parity tests from current behavior samples**
- [x] **Step 2: Verify failing**
Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PresentationNavigationOrchestratorTests"`
- [x] **Step 3: Implement orchestrator and parser (no behavior change target)**
- [x] **Step 4: Wire window entrypoints to orchestrator**
- [x] **Step 5: Re-run tests**
Expected: PASS + existing `WpsHook*` tests still green.
- [ ] **Step 6: Commit**
`git commit -m "refactor: route presentation navigation via orchestrator"`

### Task 3: Unify Debounce Ownership

**Files:**
- Create: `src/ClassroomToolkit.App/Paint/PresentationNavigationDebounceCoordinator.cs`
- Modify: `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Presentation.cs`
- Modify: `src/ClassroomToolkit.Services/Presentation/PresentationControlService.cs`
- Test: `tests/ClassroomToolkit.Tests/PresentationNavigationDebounceCoordinatorTests.cs`
- Test: `tests/ClassroomToolkit.Tests/PresentationControlServiceTests.cs`

- [x] **Step 1: Write failing tests proving single-owner debounce semantics**
- [x] **Step 2: Verify failing**
- [x] **Step 3: Move debounce checks to coordinator and remove overlap** (phase-1: policy + hook-source single debounce semantics)
- [x] **Step 4: Re-run targeted tests**
Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~Debounce|FullyQualifiedName~PresentationControlServiceTests"`
- [ ] **Step 5: Commit**
`git commit -m "refactor: unify presentation navigation debounce ownership"`

### Task 4: Dispatch Priority Policy Isolation

**Files:**
- Create: `src/ClassroomToolkit.App/Paint/PresentationNavigationDispatchPriorityPolicy.cs`
- Modify: `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Presentation.cs`
- Test: `tests/ClassroomToolkit.Tests/PresentationNavigationDispatchPriorityPolicyTests.cs`
- Modify: `tests/ClassroomToolkit.Tests/App/PaintOverlayWpsHookUnavailableContractTests.cs`

- [x] **Step 1: Write failing priority policy tests**
- [x] **Step 2: Verify failing**
- [x] **Step 3: Apply policy and remove hardcoded priority literals**
- [x] **Step 4: Re-run tests**
- [ ] **Step 5: Commit**
`git commit -m "refactor: isolate presentation hook dispatch priority policy"`

### Task 5: Add Regression Matrix Gate

**Files:**
- Create: `tests/ClassroomToolkit.Tests/PresentationNavigationRegressionMatrixTests.cs`
- Modify: `tests/ClassroomToolkit.Tests/WpsHookInterceptPolicyTests.cs`
- Modify: `tests/ClassroomToolkit.Tests/OverlayPresentationRoutingPolicyTests.cs`

- [x] **Step 1: Add failing matrix cases (PPT/WPS x brush/cursor x keyboard/wheel x foreground)**
- [x] **Step 2: Verify matrix fails before final adjustments**
- [x] **Step 3: Fix orchestrator/policy gaps to satisfy matrix**
- [x] **Step 4: Run full navigation gate**
Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PresentationNavigationRegressionMatrixTests|FullyQualifiedName~WpsHook|FullyQualifiedName~Presentation"`
- [ ] **Step 5: Commit**
`git commit -m "test: add presentation navigation regression matrix gate"`

### Task 6: Documentation and Rollback Playbook Update

**Files:**
- Modify: `docs/validation/manual-final-regression-checklist.md`
- Modify: `docs/handover.md`
- Modify: `docs/runbooks/migration-rollback-playbook.md`

- [x] **Step 1: Add new regression checklist entries and evidence template**
- [x] **Step 2: Add rollback instructions for orchestrator/debounce split**
- [x] **Step 3: Validate doc references are consistent**
- [ ] **Step 4: Commit**
`git commit -m "docs: update navigation regression and rollback playbook"`

## 6. Risk Register

- Risk: behavior drift while moving logic out of window.
Mitigation: parity tests before refactor wiring.
Rollback: restore old method body from previous commit.

- Risk: debounce consolidation changes rapid-key semantics.
Mitigation: explicit repeated-key tests with 0ms and non-0ms debounce.
Rollback: keep legacy debounce path guarded for one release cycle.

- Risk: latency optimization harms wheel flow.
Mitigation: source-specific priority policy with dedicated tests.
Rollback: revert priority policy to background for all sources.

## 7. Execution Notes

- Keep each task small; no mixed refactor + behavior changes in same commit.
- Prefer `rg` discovery and targeted tests before full suite.
- If manual validation is unavailable, block release and mark evidence gap in `docs/handover.md`.
