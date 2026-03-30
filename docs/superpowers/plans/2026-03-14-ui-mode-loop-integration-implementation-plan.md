# UI Mode Loop Integration Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a repo-local `ui-window-system` mode and mode-aware loop plumbing so ClassroomToolkit can later run unattended UI/window-system refactor work without colliding with the existing architecture-refactor loop.

**Architecture:** Keep `autonomous-execution-loop` as the generic execution kernel and evolve the repo-local `autonomous-refactor-loop` into a multi-mode adapter. Introduce a mode registry, mode-specific task/state/config files, mode-aware helper scripts, shared lock ownership with `mode_id`, and UI-mode governing reconciliation/manual-gate semantics while keeping current architecture-refactor behavior working.

**Tech Stack:** PowerShell wrappers and helper scripts, JSON task/state/config files, repo-local Codex skill adapter, existing ClassroomToolkit docs/refactor automation layout.

---

## Preconditions And Source Of Truth

- This implementation plan only covers repo-local loop plumbing owned by ClassroomToolkit.
- Generic `autonomous-execution-loop` family/bootstrap updates are an external follow-up and are not implemented by this plan unless the user explicitly asks for that second phase.
- The authoritative implementation plan path for this mode is:
  - `E:\PythonProject\ClassroomToolkit\docs\superpowers\plans\2026-03-14-ui-mode-loop-integration-implementation-plan.md`
- The authoritative UI mode governing docs for reconciliation and completion are:
  - `E:\PythonProject\ClassroomToolkit\docs\superpowers\specs\2026-03-14-ui-window-system-design.md`
  - `E:\PythonProject\ClassroomToolkit\docs\superpowers\specs\2026-03-14-ui-mode-loop-integration-design.md`
  - `E:\PythonProject\ClassroomToolkit\docs\superpowers\plans\2026-03-14-ui-mode-loop-integration-implementation-plan.md`
  - `E:\PythonProject\ClassroomToolkit\docs\validation\ui-window-system-progress.md`
  - `E:\PythonProject\ClassroomToolkit\docs\validation\ui-window-system-acceptance.md`
- Source-of-truth split:
  - `docs/ui-refactor/tasks.json` owns execution graph metadata, task-level `done_when`, `behavior_invariants`, `manual_gate`, and task-level `governing_reconciliation` pointers.
  - `.codex/ui-window-system.config.json` owns runtime policy, wrapper defaults, gate policy, stop rules, and reconciliation writeback behavior.
  - `.codex/ui-window-system-state.json` owns live execution state, blocked records, gate/freeze flags, and reconciliation stamps.
- Parameter precedence rule:
  - `-Mode` resolves the authoritative registry entry first.
  - Explicit `-TaskFile` / `-StateFile` / `-ConfigFile` overrides are only valid when they match the resolved registry mapping.
  - Mismatched explicit paths must fail with a mode/path conflict instead of silently mixing modes.

## File Structure

### New files

- `E:\PythonProject\ClassroomToolkit\.codex\refactor-modes.json`
  - Repo-local mode registry mapping `mode_id` to `mode_family`, tasks/state/config paths, governing docs, helper scripts, and stop/gate policy.
- `E:\PythonProject\ClassroomToolkit\.codex\ui-window-system.config.json`
  - UI mode runtime config including authoritative docs, manual gates, and governing reconciliation policy.
- `E:\PythonProject\ClassroomToolkit\.codex\ui-window-system-state.json`
  - UI mode state file with `mode`, `mode_family`, gate/freeze fields, blocked records, and governing reconciliation state.
- `E:\PythonProject\ClassroomToolkit\docs\ui-refactor\tasks.json`
  - UI mode execution graph scaffold with stage skeleton and initial manual gates.
- `E:\PythonProject\ClassroomToolkit\docs\superpowers\plans\2026-03-14-ui-mode-loop-integration-implementation-plan.md`
  - Authoritative implementation plan for the repo-local mode integration work.
- `E:\PythonProject\ClassroomToolkit\docs\validation\ui-window-system-progress.md`
  - Running progress log for UI mode.
- `E:\PythonProject\ClassroomToolkit\docs\validation\ui-window-system-acceptance.md`
  - Acceptance and visual gate evidence log for UI mode.
- `E:\PythonProject\ClassroomToolkit\scripts\refactor\resolve-refactor-mode.ps1`
  - Shared resolver for `-Mode` normalization, registry lookup, and path expansion.
- `E:\PythonProject\ClassroomToolkit\scripts\refactor\resume-manual-gate.ps1`
  - Repo-local helper that resumes a blocked UI gate with evidence doc writeback, state unblock, and reconciliation refresh.
- `E:\PythonProject\ClassroomToolkit\scripts\refactor\test-ui-mode-smoke.ps1`
  - Smoke test for `ui-window-system` registry resolution, selection, state mutation, and reconciliation behavior.

### Modified files

- `E:\PythonProject\ClassroomToolkit\scripts\run-refactor-loop.ps1`
  - Add `-Mode`, registry/path resolution, shared lock record with `mode_id`, mode-specific log directory, and mode-aware helper dispatch.
- `E:\PythonProject\ClassroomToolkit\scripts\run-autonomous-execution-loop.ps1`
  - Forward `-Mode` to the repo-local wrapper and stop hardcoding the architecture-refactor task/state pair.
- `E:\PythonProject\ClassroomToolkit\scripts\refactor\select-next-task.ps1`
  - Accept mode-resolved paths and keep selection semantics stable across multiple task graphs.
- `E:\PythonProject\ClassroomToolkit\scripts\refactor\update-refactor-state.ps1`
  - Support mode-specific state files, top-level `mode`/`mode_family`, gate-resume unblock behavior, and reconciliation fields.
- `E:\PythonProject\ClassroomToolkit\scripts\refactor\test-governing-reconciliation.ps1`
  - Accept mode-specific authoritative doc sets and mode-specific reconciliation state.
- `E:\PythonProject\ClassroomToolkit\scripts\refactor\check-doc-consistency.ps1`
  - Read mode-specific governing docs and refuse cross-mode consistency assumptions.
- `E:\PythonProject\ClassroomToolkit\scripts\refactor\install-refactor-adapter.ps1`
  - Install/export workflow must register the mode registry, UI state seed, and UI helper artifacts.
- `E:\PythonProject\ClassroomToolkit\scripts\refactor\export-refactor-adapter.ps1`
  - Export workflow must include UI mode files in adapter packaging/output manifests.
- `E:\PythonProject\ClassroomToolkit\scripts\refactor\refactor-adapter.manifest.json`
  - Register new mode-aware helper files and registry artifacts.

### Existing docs to reference while implementing

- `E:\PythonProject\ClassroomToolkit\docs\superpowers\specs\2026-03-14-ui-window-system-design.md`
- `E:\PythonProject\ClassroomToolkit\docs\superpowers\specs\2026-03-14-ui-mode-loop-integration-design.md`
- `E:\PythonProject\ClassroomToolkit\docs\superpowers\plans\2026-03-14-ui-mode-loop-integration-implementation-plan.md`
- `E:\PythonProject\ClassroomToolkit\docs\refactor\tasks.json`
- `E:\PythonProject\ClassroomToolkit\.codex\refactor-state.json`

## Chunk 1: Registry And Mode Resolution

### Task 1: Add the repo-local mode registry

**Files:**
- Create: `E:\PythonProject\ClassroomToolkit\.codex\refactor-modes.json`
- Test: `E:\PythonProject\ClassroomToolkit\scripts\refactor\test-ui-mode-smoke.ps1`

- [ ] **Step 1: Write the failing smoke assertion for registry presence and fields**

```powershell
$registry = Get-Content '.codex/refactor-modes.json' -Raw | ConvertFrom-Json
if (-not ($registry.modes | Where-Object mode_id -eq 'ui-window-system')) {
    throw 'ui-window-system mode missing'
}
```

- [ ] **Step 2: Run the smoke check to verify it fails**

Run:

```powershell
powershell -File scripts/refactor/test-ui-mode-smoke.ps1
```

Expected: FAIL with missing registry or missing `ui-window-system`.

- [ ] **Step 3: Add the initial registry with explicit mode metadata**

```json
{
  "version": "2026-03-14",
  "modes": [
    {
      "mode_id": "architecture-refactor",
      "mode_family": "architecture-refactor",
      "tasks_file": "docs/refactor/tasks.json",
      "state_file": ".codex/refactor-state.json",
      "config_file": null,
      "governing_docs": [],
      "verification": { "commands": [] },
      "manual_gates": [],
      "stop_rules": []
    },
    {
      "mode_id": "ui-window-system",
      "mode_family": "ui-overhaul",
      "tasks_file": "docs/ui-refactor/tasks.json",
      "state_file": ".codex/ui-window-system-state.json",
      "config_file": ".codex/ui-window-system.config.json",
      "governing_docs": [
        "docs/superpowers/specs/2026-03-14-ui-window-system-design.md",
        "docs/superpowers/specs/2026-03-14-ui-mode-loop-integration-design.md",
        "docs/superpowers/plans/2026-03-14-ui-mode-loop-integration-implementation-plan.md",
        "docs/validation/ui-window-system-progress.md",
        "docs/validation/ui-window-system-acceptance.md"
      ],
      "verification": {
        "commands": [
          "powershell -File scripts/refactor/test-ui-mode-smoke.ps1"
        ]
      },
      "manual_gates": [
        "theme-freeze",
        "main-scene-freeze",
        "fullscreen-float-freeze",
        "final-visual-regression"
      ],
      "stop_rules": [
        "BLOCKED_NEEDS_HUMAN",
        "NO_ELIGIBLE_TASK",
        "ALL_AUTOMATABLE_TASKS_DONE"
      ]
    }
  ]
}
```

- [ ] **Step 4: Run the smoke check to verify it passes**

Run:

```powershell
powershell -File scripts/refactor/test-ui-mode-smoke.ps1
```

Expected: PASS for registry discovery.

- [ ] **Step 5: Checkpoint commit only if user explicitly requests commit**

```bash
git add .codex/refactor-modes.json scripts/refactor/test-ui-mode-smoke.ps1
git commit -m "feat: add repo-local mode registry"
```

### Task 2: Add mode normalization and lookup helper

**Files:**
- Create: `E:\PythonProject\ClassroomToolkit\scripts\refactor\resolve-refactor-mode.ps1`
- Modify: `E:\PythonProject\ClassroomToolkit\scripts\run-refactor-loop.ps1`
- Test: `E:\PythonProject\ClassroomToolkit\scripts\refactor\test-ui-mode-smoke.ps1`

- [ ] **Step 1: Extend smoke test with family-to-mode and concrete-mode assertions**

```powershell
$resolvedFamily = & powershell -File scripts/refactor/resolve-refactor-mode.ps1 -Mode ui-overhaul -AsJson | ConvertFrom-Json
if ($resolvedFamily.mode_id -ne 'ui-window-system') { throw 'family mapping failed' }
```

- [ ] **Step 2: Run the smoke test to verify it fails**

Run:

```powershell
powershell -File scripts/refactor/test-ui-mode-smoke.ps1
```

Expected: FAIL because resolver script does not exist yet.

- [ ] **Step 3: Implement the resolver**

```powershell
param([string]$Mode, [switch]$AsJson)

$registry = Get-Content '.codex/refactor-modes.json' -Raw | ConvertFrom-Json
$match = $registry.modes | Where-Object { $_.mode_id -eq $Mode -or $_.mode_family -eq $Mode }
if (@($match).Count -ne 1) { throw 'Mode resolution is ambiguous or missing.' }
$result = [pscustomobject]@{
    mode_id = $match[0].mode_id
    mode_family = $match[0].mode_family
    tasks_file = $match[0].tasks_file
    state_file = $match[0].state_file
    config_file = $match[0].config_file
}
if ($AsJson) { $result | ConvertTo-Json -Depth 20 } else { $result }
```

- [ ] **Step 4: Run the smoke test to verify it passes**

Run:

```powershell
powershell -File scripts/refactor/test-ui-mode-smoke.ps1
```

Expected: PASS for both `ui-overhaul` and `ui-window-system` inputs.

- [ ] **Step 5: Checkpoint commit only if user explicitly requests commit**

```bash
git add scripts/refactor/resolve-refactor-mode.ps1 scripts/refactor/test-ui-mode-smoke.ps1 scripts/run-refactor-loop.ps1
git commit -m "feat: add mode resolver for refactor loop"
```

### Task 3: Add UI mode config/state/task graph scaffolding

**Files:**
- Create: `E:\PythonProject\ClassroomToolkit\.codex\ui-window-system.config.json`
- Create: `E:\PythonProject\ClassroomToolkit\.codex\ui-window-system-state.json`
- Create: `E:\PythonProject\ClassroomToolkit\docs\ui-refactor\tasks.json`
- Create: `E:\PythonProject\ClassroomToolkit\docs\validation\ui-window-system-progress.md`
- Create: `E:\PythonProject\ClassroomToolkit\docs\validation\ui-window-system-acceptance.md`
- Test: `E:\PythonProject\ClassroomToolkit\scripts\refactor\test-ui-mode-smoke.ps1`

- [ ] **Step 1: Extend smoke test with checks for UI mode files and key fields**

```powershell
$state = Get-Content '.codex/ui-window-system-state.json' -Raw | ConvertFrom-Json
if ($state.mode -ne 'ui-window-system') { throw 'wrong UI mode id' }
if ($state.mode_family -ne 'ui-overhaul') { throw 'wrong UI mode family' }
```

- [ ] **Step 2: Run the smoke test to verify it fails**

Run:

```powershell
powershell -File scripts/refactor/test-ui-mode-smoke.ps1
```

Expected: FAIL because UI mode files do not exist yet.

- [ ] **Step 3: Add the initial UI mode files with authoritative scaffolding**

```json
{
  "mode": "ui-window-system",
  "mode_family": "ui-overhaul",
  "current_task": null,
  "visual_phase": "foundation",
  "current_manual_gate": null,
  "theme_frozen": false,
  "main_scene_frozen": false,
  "fullscreen_frozen": false,
  "final_visual_review_passed": false,
  "governing_reconciliation": {
    "last_reconciled_at": null,
    "doc_paths": [
      "docs/superpowers/specs/2026-03-14-ui-window-system-design.md",
      "docs/superpowers/specs/2026-03-14-ui-mode-loop-integration-design.md",
      "docs/superpowers/plans/2026-03-14-ui-mode-loop-integration-implementation-plan.md",
      "docs/validation/ui-window-system-progress.md",
      "docs/validation/ui-window-system-acceptance.md"
    ],
    "doc_sync_policy": "spec > integration-design > implementation-plan > progress > acceptance",
    "completion_gate_policy": "refuse_done_when_authoritative_docs_are_newer_than_reconciliation"
  },
  "tasks": {},
  "blocked": [],
  "history": []
}
```

```json
{
  "project": "ClassroomToolkit",
  "updated_at": "2026-03-14",
  "mode": "ui-window-system",
  "scope": {
    "goal": "unattended-safe loop plumbing for UI/window-system refactor preparation"
  },
  "wrapper": {
    "default_manual_stop_status": "BLOCKED_NEEDS_HUMAN",
    "shared_lock_file": ".codex/refactor-loop.lock.json"
  },
  "manual_gates": [
    "theme-freeze",
    "main-scene-freeze",
    "fullscreen-float-freeze",
    "final-visual-regression"
  ],
  "stop_rules": [
    "BLOCKED_NEEDS_HUMAN",
    "NO_ELIGIBLE_TASK",
    "ALL_AUTOMATABLE_TASKS_DONE"
  ],
  "governing_reconciliation": {
    "doc_paths": [
      "docs/superpowers/specs/2026-03-14-ui-window-system-design.md",
      "docs/superpowers/specs/2026-03-14-ui-mode-loop-integration-design.md",
      "docs/superpowers/plans/2026-03-14-ui-mode-loop-integration-implementation-plan.md",
      "docs/validation/ui-window-system-progress.md",
      "docs/validation/ui-window-system-acceptance.md"
    ],
    "doc_sync_policy": "spec > integration-design > implementation-plan > progress > acceptance",
    "refresh_on_gate_resume": true
  }
}
```

```json
{
  "mode": "ui-window-system",
  "mode_family": "ui-overhaul",
  "governing_reconciliation": {
    "doc_paths": [
      "docs/superpowers/specs/2026-03-14-ui-window-system-design.md",
      "docs/superpowers/specs/2026-03-14-ui-mode-loop-integration-design.md",
      "docs/superpowers/plans/2026-03-14-ui-mode-loop-integration-implementation-plan.md",
      "docs/validation/ui-window-system-progress.md",
      "docs/validation/ui-window-system-acceptance.md"
    ],
    "doc_sync_policy": "spec > integration-design > implementation-plan > progress > acceptance"
  },
  "tasks": [
    {
      "id": "ui-foundation-bootstrap",
      "title": "Bootstrap UI mode execution layer",
      "stage": "foundation",
      "priority": 1,
      "order": 10,
      "depends_on": [],
      "file_hints": [
        ".codex/refactor-modes.json",
        ".codex/ui-window-system.config.json",
        ".codex/ui-window-system-state.json",
        "scripts/refactor/resolve-refactor-mode.ps1"
      ],
      "done_when": [
        "repo-local registry resolves ui-window-system deterministically",
        "ui mode state/config/task graph files exist with baseline schema",
        "no existing architecture-refactor path is rebound to UI files"
      ],
      "verify": {
        "commands": [
          "powershell -File scripts/refactor/test-ui-mode-smoke.ps1"
        ]
      },
      "manual_gate": null,
      "behavior_invariants": [
        "ui-window-system must not reuse docs/refactor/tasks.json",
        "ui-window-system must not reuse .codex/refactor-state.json"
      ],
      "blocked_by_visual_review": false
    }
  ]
}
```

- [ ] **Step 4: Run the smoke test to verify it passes**

Run:

```powershell
powershell -File scripts/refactor/test-ui-mode-smoke.ps1
```

Expected: PASS for file existence and baseline fields.

- [ ] **Step 5: Checkpoint commit only if user explicitly requests commit**

```bash
git add .codex/ui-window-system.config.json .codex/ui-window-system-state.json docs/ui-refactor/tasks.json docs/validation/ui-window-system-progress.md docs/validation/ui-window-system-acceptance.md scripts/refactor/test-ui-mode-smoke.ps1
git commit -m "feat: scaffold ui-window-system mode files"
```

## Chunk 2: Mode-Aware Adapter Scripts

### Task 4: Make task selection mode-aware

**Files:**
- Modify: `E:\PythonProject\ClassroomToolkit\scripts\refactor\select-next-task.ps1`
- Test: `E:\PythonProject\ClassroomToolkit\scripts\refactor\test-ui-mode-smoke.ps1`

- [ ] **Step 1: Add a smoke case that selects the first UI task**

```powershell
$selection = & powershell -File scripts/refactor/select-next-task.ps1 -TaskFile docs/ui-refactor/tasks.json -StateFile .codex/ui-window-system-state.json -AsJson | ConvertFrom-Json
if ($selection.status -eq 'blocked') { throw 'UI mode should have a selectable initial task' }
```

- [ ] **Step 2: Run the smoke test to verify it fails**

Run:

```powershell
powershell -File scripts/refactor/test-ui-mode-smoke.ps1
```

Expected: FAIL until `docs/ui-refactor/tasks.json` has a valid initial task and selector handles it.

- [ ] **Step 3: Adjust `docs/ui-refactor/tasks.json` and selector logic with UI schema preservation**

```json
{
  "mode": "ui-window-system",
  "mode_family": "ui-overhaul",
  "governing_reconciliation": {
    "doc_paths": [
      "docs/superpowers/specs/2026-03-14-ui-window-system-design.md",
      "docs/superpowers/specs/2026-03-14-ui-mode-loop-integration-design.md",
      "docs/superpowers/plans/2026-03-14-ui-mode-loop-integration-implementation-plan.md",
      "docs/validation/ui-window-system-progress.md",
      "docs/validation/ui-window-system-acceptance.md"
    ],
    "doc_sync_policy": "spec > integration-design > implementation-plan > progress > acceptance"
  },
  "tasks": [
    {
      "id": "ui-foundation-bootstrap",
      "title": "Bootstrap UI mode execution layer",
      "stage": "foundation",
      "priority": 1,
      "order": 10,
      "depends_on": [],
      "file_hints": [
        ".codex/refactor-modes.json",
        ".codex/ui-window-system-state.json"
      ],
      "done_when": [
        "selector can claim the first UI task without touching architecture state"
      ],
      "manual_gate": null,
      "behavior_invariants": [
        "selector must honor the supplied TaskFile and StateFile pair",
        "selector must not infer architecture mode from legacy defaults"
      ],
      "blocked_by_visual_review": false,
      "verify": { "commands": [] }
    }
  ]
}
```

- [ ] **Step 4: Run the smoke test to verify it passes**

Run:

```powershell
powershell -File scripts/refactor/test-ui-mode-smoke.ps1
```

Expected: PASS with `ui-foundation-bootstrap` selected.

- [ ] **Step 5: Checkpoint commit only if user explicitly requests commit**

```bash
git add scripts/refactor/select-next-task.ps1 docs/ui-refactor/tasks.json scripts/refactor/test-ui-mode-smoke.ps1
git commit -m "feat: support ui mode task selection"
```

### Task 5: Make state updates mode-aware, including gate resume

**Files:**
- Modify: `E:\PythonProject\ClassroomToolkit\scripts\refactor\update-refactor-state.ps1`
- Create: `E:\PythonProject\ClassroomToolkit\scripts\refactor\resume-manual-gate.ps1`
- Test: `E:\PythonProject\ClassroomToolkit\scripts\refactor\test-ui-mode-smoke.ps1`

- [ ] **Step 1: Add a failing smoke case for gate resume**

```powershell
& powershell -File scripts/refactor/update-refactor-state.ps1 -Action block -StateFile .codex/ui-window-system-state.json -TaskId ui-foundation-bootstrap -Summary 'waiting for theme freeze' -Reason 'visual_gate'
& powershell -File scripts/refactor/resume-manual-gate.ps1 -Mode ui-window-system -GateId theme-freeze -TaskId ui-foundation-bootstrap -EvidenceDoc docs/validation/ui-window-system-acceptance.md
$state = Get-Content '.codex/ui-window-system-state.json' -Raw | ConvertFrom-Json
if ($state.tasks.'ui-foundation-bootstrap'.status -ne 'pending') { throw 'task not resumed to pending' }
```

- [ ] **Step 2: Run the smoke test to verify it fails**

Run:

```powershell
powershell -File scripts/refactor/test-ui-mode-smoke.ps1
```

Expected: FAIL because gate resume helper does not exist and blocked cleanup is missing.

- [ ] **Step 3: Add minimal state and gate-resume support**

```powershell
# update-refactor-state.ps1 additions
state.mode = 'ui-window-system'
state.mode_family = 'ui-overhaul'

# resume-manual-gate.ps1 behavior
# - clear current_manual_gate
# - set theme_frozen/main_scene_frozen/etc.
# - remove matching blocked entry
# - set task back to pending
# - append history entry with evidence_doc
```

- [ ] **Step 4: Run the smoke test to verify it passes**

Run:

```powershell
powershell -File scripts/refactor/test-ui-mode-smoke.ps1
```

Expected: PASS with task restored to `pending`, blocked record removed, and history updated.

- [ ] **Step 5: Checkpoint commit only if user explicitly requests commit**

```bash
git add scripts/refactor/update-refactor-state.ps1 scripts/refactor/resume-manual-gate.ps1 scripts/refactor/test-ui-mode-smoke.ps1
git commit -m "feat: add ui mode state updates and gate resume"
```

### Task 6: Make governing reconciliation mode-aware

**Files:**
- Modify: `E:\PythonProject\ClassroomToolkit\scripts\refactor\test-governing-reconciliation.ps1`
- Modify: `E:\PythonProject\ClassroomToolkit\.codex\ui-window-system.config.json`
- Test: `E:\PythonProject\ClassroomToolkit\scripts\refactor\test-ui-mode-smoke.ps1`

- [ ] **Step 1: Add a smoke case for UI authoritative docs reconciliation**

```powershell
$recon = & powershell -File scripts/refactor/test-governing-reconciliation.ps1 -TaskFile docs/ui-refactor/tasks.json -AsJson | ConvertFrom-Json
if ($recon.status -notin @('ok','needs_reconciliation')) { throw 'unexpected reconciliation status' }
```

- [ ] **Step 2: Run the smoke test to verify it fails**

Run:

```powershell
powershell -File scripts/refactor/test-ui-mode-smoke.ps1
```

Expected: FAIL because UI task graph/config lacks full reconciliation metadata.

- [ ] **Step 3: Add task-graph reconciliation metadata and runtime reconciliation policy**

```json
{
  "governing_reconciliation": {
    "last_reconciled_at": "2026-03-14T00:00:00Z",
    "doc_paths": [
      "docs/superpowers/specs/2026-03-14-ui-window-system-design.md",
      "docs/superpowers/specs/2026-03-14-ui-mode-loop-integration-design.md",
      "docs/superpowers/plans/2026-03-14-ui-mode-loop-integration-implementation-plan.md",
      "docs/validation/ui-window-system-progress.md",
      "docs/validation/ui-window-system-acceptance.md"
    ],
    "doc_sync_policy": "spec > integration-design > implementation-plan > progress > acceptance",
    "completion_gate_policy": "refuse_done_when_authoritative_docs_are_newer_than_reconciliation"
  }
}
```

```json
{
  "wrapper": {
    "reconciliation_refresh_on_gate_resume": true,
    "refuse_completion_when_reconciliation_stale": true
  }
}
```

- [ ] **Step 4: Run the smoke test to verify it passes**

Run:

```powershell
powershell -File scripts/refactor/test-ui-mode-smoke.ps1
```

Expected: PASS with mode-aware reconciliation output.

- [ ] **Step 5: Checkpoint commit only if user explicitly requests commit**

```bash
git add scripts/refactor/test-governing-reconciliation.ps1 .codex/ui-window-system.config.json docs/ui-refactor/tasks.json scripts/refactor/test-ui-mode-smoke.ps1
git commit -m "feat: add ui mode governing reconciliation"
```

### Task 7: Add runtime policy config and mode/path conflict checks

**Files:**
- Modify: `E:\PythonProject\ClassroomToolkit\.codex\ui-window-system.config.json`
- Modify: `E:\PythonProject\ClassroomToolkit\scripts\run-refactor-loop.ps1`
- Test: `E:\PythonProject\ClassroomToolkit\scripts\refactor\test-ui-mode-smoke.ps1`

- [ ] **Step 1: Add a failing smoke case for explicit path conflict**

```powershell
& powershell -File scripts/run-refactor-loop.ps1 -Mode ui-window-system -TaskFile docs/refactor/tasks.json -DryRun
if ($LASTEXITCODE -eq 0) { throw 'wrapper must reject mode/path conflicts' }
```

- [ ] **Step 2: Run the smoke test to verify it fails**

Run:

```powershell
powershell -File scripts/refactor/test-ui-mode-smoke.ps1
```

Expected: FAIL until wrapper validates explicit path overrides against registry mapping.

- [ ] **Step 3: Add runtime policy and conflict checks**

```json
{
  "wrapper": {
    "allow_explicit_path_override_only_when_matching_registry": true,
    "reconciliation_refresh_on_gate_resume": true,
    "refuse_completion_when_reconciliation_stale": true
  }
}
```

- [ ] **Step 4: Run the smoke test to verify it passes**

Run:

```powershell
powershell -File scripts/refactor/test-ui-mode-smoke.ps1
```

Expected: PASS with mismatched path overrides rejected and matching overrides accepted.

- [ ] **Step 5: Checkpoint commit only if user explicitly requests commit**

```bash
git add .codex/ui-window-system.config.json scripts/run-refactor-loop.ps1 scripts/refactor/test-ui-mode-smoke.ps1
git commit -m "feat: add ui mode runtime policy checks"
```

## Chunk 3: Wrappers, Locking, And Manifest

### Task 8: Make the repo-local wrapper mode-aware

**Files:**
- Modify: `E:\PythonProject\ClassroomToolkit\scripts\run-refactor-loop.ps1`
- Modify: `E:\PythonProject\ClassroomToolkit\scripts\run-autonomous-execution-loop.ps1`
- Test: `E:\PythonProject\ClassroomToolkit\scripts\refactor\test-ui-mode-smoke.ps1`

- [ ] **Step 1: Add a failing smoke case for `-Mode ui-window-system`**

```powershell
& powershell -File scripts/run-refactor-loop.ps1 -Mode ui-window-system -DryRun
if ($LASTEXITCODE -ne 0) { throw 'mode-aware wrapper dry run failed' }
```

- [ ] **Step 2: Run the smoke test to verify it fails**

Run:

```powershell
powershell -File scripts/refactor/test-ui-mode-smoke.ps1
```

Expected: FAIL because wrappers do not accept `-Mode`.

- [ ] **Step 3: Add `-Mode` plumbing**

```powershell
param(
    [string]$Mode = 'architecture-refactor'
)

$modeInfo = & powershell -File $modeResolverPath -Mode $Mode -AsJson | ConvertFrom-Json
$TaskFile = $modeInfo.tasks_file
$StateFile = $modeInfo.state_file
```

- [ ] **Step 4: Run the smoke test to verify it passes**

Run:

```powershell
powershell -File scripts/refactor/test-ui-mode-smoke.ps1
```

Expected: PASS for wrapper dry-run argument resolution.

- [ ] **Step 5: Checkpoint commit only if user explicitly requests commit**

```bash
git add scripts/run-refactor-loop.ps1 scripts/run-autonomous-execution-loop.ps1 scripts/refactor/test-ui-mode-smoke.ps1
git commit -m "feat: add mode-aware loop wrappers"
```

### Task 9: Add shared lock ownership with `mode_id`

**Files:**
- Modify: `E:\PythonProject\ClassroomToolkit\scripts\run-refactor-loop.ps1`
- Test: `E:\PythonProject\ClassroomToolkit\scripts\refactor\test-ui-mode-smoke.ps1`

- [ ] **Step 1: Add a failing smoke case for lock metadata**

```powershell
$lock = Get-Content '.codex/refactor-loop.lock.json' -Raw | ConvertFrom-Json
if (-not $lock.mode_id) { throw 'mode_id missing from lock' }
if (-not $lock.loop_run_id) { throw 'loop_run_id missing from lock' }
```

- [ ] **Step 2: Run the smoke test to verify it fails**

Run:

```powershell
powershell -File scripts/refactor/test-ui-mode-smoke.ps1
```

Expected: FAIL because lock shape is not yet mode-aware.

- [ ] **Step 3: Extend the existing lock record without breaking ownership semantics**

```powershell
$lockRecord = [ordered]@{
    owner_kind = 'wrapper'
    loop_run_id = $loopRunId
    pid = $PID
    started_at = [DateTime]::UtcNow.ToString('o')
    mode_id = $modeInfo.mode_id
    mode_family = $modeInfo.mode_family
    task_file = $TaskFile
    state_file = $StateFile
    config_file = $modeInfo.config_file
}
```

- [ ] **Step 4: Run the smoke test to verify it passes**

Run:

```powershell
powershell -File scripts/refactor/test-ui-mode-smoke.ps1
```

Expected: PASS with shared lock containing existing ownership fields plus mode metadata.

- [ ] **Step 5: Checkpoint commit only if user explicitly requests commit**

```bash
git add scripts/run-refactor-loop.ps1 scripts/refactor/test-ui-mode-smoke.ps1
git commit -m "feat: add mode-aware shared loop lock"
```

### Task 10: Make helper scripts, installer/exporter, and manifest mode-aware

**Files:**
- Modify: `E:\PythonProject\ClassroomToolkit\scripts\refactor\check-doc-consistency.ps1`
- Modify: `E:\PythonProject\ClassroomToolkit\scripts\refactor\install-refactor-adapter.ps1`
- Modify: `E:\PythonProject\ClassroomToolkit\scripts\refactor\export-refactor-adapter.ps1`
- Modify: `E:\PythonProject\ClassroomToolkit\scripts\refactor\refactor-adapter.manifest.json`
- Modify: `E:\PythonProject\ClassroomToolkit\scripts\refactor\test-ui-mode-smoke.ps1`

- [ ] **Step 1: Add a failing smoke case for helper/manifest registration**

```powershell
$manifest = Get-Content 'scripts/refactor/refactor-adapter.manifest.json' -Raw | ConvertFrom-Json
if (-not ($manifest.entries | Where-Object path -eq '.codex/refactor-modes.json')) { throw 'registry missing from manifest' }
```

- [ ] **Step 2: Run the smoke test to verify it fails**

Run:

```powershell
powershell -File scripts/refactor/test-ui-mode-smoke.ps1
```

Expected: FAIL because new files are not registered.

- [ ] **Step 3: Update manifest, install/export workflows, and consistency script**

```json
{
  "path": ".codex/refactor-modes.json",
  "kind": "file",
  "required": true,
  "role": "mode-registry"
}
```

```json
{
  "path": ".codex/ui-window-system-state.json",
  "kind": "seed-state",
  "required": true,
  "role": "ui-mode-state"
}
```

- [ ] **Step 4: Run the smoke test to verify it passes**

Run:

```powershell
powershell -File scripts/refactor/test-ui-mode-smoke.ps1
```

Expected: PASS with all UI mode artifacts discoverable and helper references valid.

- [ ] **Step 5: Checkpoint commit only if user explicitly requests commit**

```bash
git add scripts/refactor/check-doc-consistency.ps1 scripts/refactor/refactor-adapter.manifest.json scripts/refactor/test-ui-mode-smoke.ps1
git commit -m "feat: register ui mode helper artifacts"
```

### Task 11: Final end-to-end dry-run verification

**Files:**
- Verify: `E:\PythonProject\ClassroomToolkit\scripts\run-refactor-loop.ps1`
- Verify: `E:\PythonProject\ClassroomToolkit\scripts\run-autonomous-execution-loop.ps1`
- Verify: `E:\PythonProject\ClassroomToolkit\scripts\refactor\test-ui-mode-smoke.ps1`

- [ ] **Step 1: Run the mode smoke script**

Run:

```powershell
powershell -File scripts/refactor/test-ui-mode-smoke.ps1
```

Expected: PASS

- [ ] **Step 2: Run wrapper dry-run for architecture mode**

Run:

```powershell
powershell -File scripts/run-refactor-loop.ps1 -Mode architecture-refactor -DryRun
```

Expected: EXIT 0, no mode-regression for existing path.

- [ ] **Step 3: Run wrapper dry-run for UI mode**

Run:

```powershell
powershell -File scripts/run-refactor-loop.ps1 -Mode ui-window-system -DryRun
```

Expected: EXIT 0, resolves UI task/state/config and stops cleanly.

- [ ] **Step 4: Run wrapper-entry dry-run for UI mode**

Run:

```powershell
powershell -File scripts/run-autonomous-execution-loop.ps1 -Mode ui-window-system -DryRun
```

Expected: EXIT 0, forwards to repo-local wrapper with UI mode intact.

- [ ] **Step 5: Run negative and reconciliation edge-case checks**

Run:

```powershell
powershell -File scripts/run-refactor-loop.ps1 -Mode ui-overhaul -DryRun
powershell -File scripts/run-refactor-loop.ps1 -Mode unsupported-family -DryRun
powershell -File scripts/refactor/test-ui-mode-smoke.ps1 -Scenario lock-contention
powershell -File scripts/refactor/test-ui-mode-smoke.ps1 -Scenario gate-resume-refresh
powershell -File scripts/refactor/test-ui-mode-smoke.ps1 -Scenario stale-reconciliation
powershell -File scripts/refactor/test-ui-mode-smoke.ps1 -Scenario installer-exporter
```

Expected:
- `ui-overhaul` resolves to `ui-window-system`
- unsupported family stops with `BLOCKED_NEEDS_HUMAN`
- lock contention is detected without corrupting either mode state
- gate resume refreshes reconciliation and keeps task in `pending`
- stale reconciliation blocks `ALL_AUTOMATABLE_TASKS_DONE`
- installer/exporter outputs include registry and UI seed-state artifacts

- [ ] **Step 6: Record explicit boundary for generic follow-up**

Add a short note to the progress log:

```markdown
Generic `autonomous-execution-loop` family bootstrap changes remain external/system-owned follow-up and are not part of this repo-local implementation pass.
```

- [ ] **Step 7: Checkpoint commit only if user explicitly requests commit**

```bash
git add .codex/refactor-modes.json .codex/ui-window-system.config.json .codex/ui-window-system-state.json docs/ui-refactor/tasks.json docs/validation/ui-window-system-progress.md docs/validation/ui-window-system-acceptance.md scripts/run-refactor-loop.ps1 scripts/run-autonomous-execution-loop.ps1 scripts/refactor/*.ps1 scripts/refactor/refactor-adapter.manifest.json
git commit -m "feat: add ui window system loop mode"
```

Plan complete and saved to `docs/superpowers/plans/2026-03-14-ui-mode-loop-integration-implementation-plan.md`. Ready to execute?
