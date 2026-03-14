---
name: autonomous-refactor-loop
description: Repo-local ClassroomToolkit refactor orchestration skill. Use it when the user wants Codex to continue the target-architecture refactor from tasks and state, run the next refactor task automatically, resume the refactor loop, or let Codex split and reorder the execution graph in `docs/refactor/tasks.json` while keeping the governing docs unchanged.
---

# Autonomous Refactor Loop

This skill is specific to the ClassroomToolkit repository.

## Core Role

You are a single-iteration autonomous refactor executor for the execution graph, not for the governing architecture docs.

Each run must do all of the following:

1. validate execution preflight and current on-disk state
2. identify the selected task
3. decide whether the task is already done
4. complete the smallest safe closure path if it is not done
5. run mandatory verification
6. update `.codex/refactor-state.json`
7. update `docs/refactor/tasks.json` only if the execution graph must change
8. emit machine-readable exit status for the wrapper

## Task Fit Gate

Before doing repository work, decide whether the requested work actually belongs in this skill.

This skill is suitable only when all of the following are true:

- the work is repo-local and should be performed inside this repository
- the work maps to the execution graph or can be safely represented there
- the work has a credible automated verification path or a safe manual-only omission path
- the work is planned, stateful, or iterative rather than a one-off question or ad hoc edit

This skill is not suitable for:

- general Q&A with no execution graph impact
- tasks that are purely manual and have no unattended-safe closure path
- work that requires new architecture decisions without governing-doc support
- requests that have no clear repository target or no verifiable completion criteria

If the request is unsuitable:

- do not mutate `docs/refactor/tasks.json`
- do not mutate `.codex/refactor-state.json`
- explain the mismatch in `EXECUTION_PLAN` and `RESULT_SUMMARY`
- emit `STATUS: BLOCKED_NEEDS_HUMAN`

If the request is suitable but prerequisites are missing:

- list the missing requirements explicitly
- do not force bootstrap or execution past unsafe ambiguity
- emit `STATUS: BLOCKED_NEEDS_HUMAN` when user action is required
- emit `STATUS: NO_ELIGIBLE_TASK` only when there is simply no eligible automated task right now

## Two-Layer Model

Treat the repository as two different layers.

### Layer 1: Governing Docs

These define architecture truth and execution truth:

- main plan
- progress doc
- handover
- final acceptance doc
- boundary map
- interop matrix
- rollback runbook

You may read them and sync them minimally when reality changes.

You must not freely restructure their meaning.

### Layer 2: Execution Graph

This is the mutable execution layer:

- `docs/refactor/tasks.json`
- `.codex/refactor-state.json`

You are allowed to adjust this layer to keep the unattended loop moving.

## Inputs And Reading Strategy

Do not always read all large governing docs upfront.

### Stage 1: Read Execution Layer First

Read these first, in order:

1. `docs/refactor/tasks.json`
2. `.codex/refactor-state.json`

If available, also inspect the repo-local run lock record:

- `.codex/refactor-loop.lock.json`

### Stage 2: Resolve Task Scope

Identify the injected task from the external supervisor. If no task is injected, select a candidate from the execution graph.

Only after task scope is known, read the minimum governing docs required for:

- the selected task's stage
- the selected task's file or boundary scope
- the selected task's `doc_sync`
- architecture guards
- rollback or migration safety

### Stage 3: Escalate Reading Only When Needed

Read the necessary governing docs before proceeding if:

- the state graph is inconsistent
- task scope cannot be resolved safely
- a dependency or omitted/manual dependency is ambiguous
- architecture truth and execution truth appear to conflict
- rollback requirements may apply

Do not use lazy loading to bypass architecture truth.

## Governing Docs By Need

Read these when task scope or verification requires them:

- `docs/plans/2026-03-06-best-target-architecture-plan.md`
- `docs/validation/2026-03-06-target-architecture-progress.md`
- `docs/handover.md`
- `docs/validation/target-architecture-final-acceptance.md`
- `docs/architecture/2026-03-10-target-boundary-map.md`
- `docs/architecture/2026-03-10-interop-direct-dependency-matrix.md`
- `docs/runbooks/migration-rollback-playbook.md`
- `tests/ClassroomToolkit.Tests/ArchitectureDependencyTests.cs`

## Preflight

Before task execution, validate all of the following against the latest on-disk state:

1. `docs/refactor/tasks.json` exists and is valid JSON
2. `.codex/refactor-state.json` exists and is valid JSON
3. required top-level fields exist or can be safely repaired without schema drift
4. selected task still exists
5. selected task is still eligible according to current state and dependencies
6. no other active executor appears to own the loop
7. the requested work is suitable for this skill
8. required prerequisites for the selected task are present, or the missing ones are explicitly reported

Re-read current state from disk:

- before selection
- before any execution-layer writeback
- before final completion or blocker writeback

Do not rely on stale in-memory snapshots across iterations.

## Concurrency And Stale-Run Safety

Do not rely only on `current_task`.

Use a repo-local lock or ownership record when available:

- `.codex/refactor-loop.lock.json`

Treat it as an execution ownership hint for active-run detection and stale-run detection.

Rules:

- if another active executor appears to own the loop, halt
- if lock ownership is ambiguous, halt
- if state suggests a stale crashed run and ownership cannot be resolved safely, halt and require human review
- never allow two concurrent writers to mutate execution state

Do not silently take over ambiguous ownership.

If the wrapper explicitly tells you that the active lock belongs to the current wrapper-owned loop:

- treat that lock as same-loop ownership, not as a foreign concurrent executor
- do not block solely because the wrapper PID differs from the child executor PID
- only block when the lock points to a different live owner than the wrapper-declared owner

## Allowed Execution-Graph Mutations

You may update `docs/refactor/tasks.json` in these ways:

- split an oversized active task into 2-5 smaller child tasks
- reorder tasks inside the same stage when dependencies still hold
- mark a task omitted when it is explicitly outside the current unattended scope
- mark a task completed when code and verification prove it is already done
- add a follow-up tail task when a broad task closes but one residual automated subproblem remains
- collapse duplicate tasks into one canonical task when they represent the same closure target

You must preserve these invariants:

- never break `depends_on`
- never modify governing docs just to make the execution graph simpler
- never hide a real blocker by deleting the task
- never convert a manual-only task into an automated-complete task without evidence
- never treat all omitted dependencies as automatically satisfied
- never rename or reshape top-level JSON schema casually

## Task Selection

Prefer the task selected by the external supervisor. If no task is injected, select from `docs/refactor/tasks.json` using the latest on-disk state.

Selection rules:

- skip `completed`
- skip `blocked`
- skip `deferred`
- skip `omitted`
- skip tasks whose dependencies are not satisfied
- prefer resumed `in_progress` work only when ownership is clearly yours and state is not stale
- sort by `priority`, then `order`

Dependency rules:

- a `completed` dependency is satisfied
- a `blocked` dependency is not satisfied
- a `deferred` dependency is not satisfied unless a semantically correct follow-up child or tail task now carries the closure requirement
- an `omitted` dependency may be treated as satisfied only if it is explicitly outside unattended scope or manual-only, and the downstream task's `done_when` does not require deliverables from it

## Iteration Protocol

### Step 1: Preflight And State Sync

- validate JSON integrity and required fields
- validate task eligibility using current on-disk state
- repair only minimal safe missing state fields
- if ownership is safe and task execution will proceed, mark the selected task as `in_progress`
- set `current_task`
- increment the attempt count

### Step 2: Emit `EXECUTION_PLAN`

Before modifying files, emit a concise structured execution log:

`EXECUTION_PLAN`

Include only operationally useful information:

- selected task id and title
- why it is eligible
- whether this is resume or fresh work
- files expected to change
- verification commands to run
- governing docs you chose to read

Do not reveal private chain-of-thought.

Use this exact line-oriented format:

```text
EXECUTION_PLAN
task_id: <id>
task_title: <title>
task_fit: <suitable|unsuitable>
prereq_status: <ready|missing>
missing_requirements: <comma-separated items or none>
eligibility: <short reason>
mode: <resume|fresh>
files_to_change: <comma-separated paths or none>
verification: <command 1> || <command 2> || none
docs_read: <comma-separated paths or none>
next_action: <short action or none>
```

### Step 3: Check Whether The Task Is Already Done

If the task appears to be already complete:

- do not redo the work
- run the task's `verify.commands`
- classify any verification failure
- if verification passes, mark the task `completed`
- record a short evidence-based summary in state history

### Step 4: Implement The Smallest Safe Closure Path

If the task is not complete:

- change only task-relevant files
- keep following the repo's `Policy / Updater / Executor / Session` refactor direction
- do not push new logic back into hotspot files unless you are extracting old inline logic from them
- never revert unrelated existing user changes
- keep changes small enough that you can verify or safely revert them inside this iteration

### Step 5: Verify

Verification is mandatory evidence for completion.

- run the task's `verify.commands`
- add extra targeted checks only when the task is high-risk
- if verification cannot be run, classify it as `unverified`

Verification failure classes:

- `task_regression`
- `pre_existing_failure`
- `environment_failure`
- `unverified`

Classification rules:

- use `task_regression` when this iteration's changes caused the failure
- use `pre_existing_failure` when the failure is already present and unrelated to this iteration
- use `environment_failure` when tooling, environment, or unavailable dependencies prevent verification
- use `unverified` when no reliable verification path can be executed in this iteration

If verification produces `task_regression`:

1. attempt the smallest safe local repair path
2. re-run the relevant verification
3. if it still fails, revert only the changes introduced in the current iteration
4. if no smaller safe closure remains, mark the task `blocked`

Never leave known broken code from this iteration in the workspace.

Never revert unowned workspace modifications.

### Step 6: Split Only When Necessary

When a task is too large:

- mark the parent task `deferred`
- add 2-5 child tasks
- every child must include `parent_id`
- every child must include new `id`, `depends_on`, `done_when`, and `verify.commands`

Do not blindly rewrite all downstream dependencies to point to every child.

Instead:

- update downstream dependencies only when logically required
- if downstream work depends on full closure of the parent, introduce either a semantically correct terminal child dependency set or an explicit follow-up completion/tail task

The execution graph must remain semantically correct, not merely syntactically connected.

### Step 7: Sync Docs

If the task changes execution truth and defines `doc_sync`:

- update only the required docs
- keep governing-doc meaning intact
- do not rewrite unrelated history docs

### Step 8: Preserve Strict JSON Integrity

Whenever modifying:

- `docs/refactor/tasks.json`
- `.codex/refactor-state.json`

preserve strict valid JSON:

- no trailing commas
- correct quoting and escaping
- no malformed arrays or objects
- no accidental schema drift

Prefer stable field order and stable shape.

### Step 9: Update State

On success:

- mark the task `completed`
- clear `current_task`
- save a short evidence-based summary

On a real blocker:

- mark the task `blocked`
- add a blocker entry with a clear reason
- clear `current_task`

On omitted manual-only work:

- keep or set the task to `omitted`
- clear `current_task`
- record that unattended scope excludes it

### Step 10: Emit `RESULT_SUMMARY`

At the end of the iteration, emit a concise structured execution log:

`RESULT_SUMMARY`

Include only operationally useful information:

- selected task
- final status
- files changed
- verification commands executed
- verification outcome
- state updates performed
- docs updated
- next expected task if one is known

Do not reveal private chain-of-thought.

Use this exact line-oriented format:

```text
RESULT_SUMMARY
task_id: <id>
final_status: <completed|blocked|deferred|omitted|unverified>
task_fit: <suitable|unsuitable>
prereq_status: <ready|missing>
missing_requirements: <comma-separated items or none>
files_changed: <comma-separated paths or none>
verification_run: <command 1> || <command 2> || none
verification_result: <pass|task_regression|pre_existing_failure|environment_failure|unverified>
state_updates: <short summary>
doc_updates: <comma-separated paths or none>
next_task: <id or none>
next_action: <short action or none>
```

## When To Split A Task

Split only when:

- the task is too large to finish and verify safely in one iteration
- the task spans independent file clusters
- the task mixes code work, migration work, and final verification in one unit

Do not split small tasks without a concrete reason.

## When To Mark A Task Omitted

Mark a task `omitted` only when:

- the user explicitly excluded that work from the unattended loop
- the task is a real manual-regression-only task
- the omitted scope is recorded in `tasks.json`
- downstream tasks do not require deliverables from it

Current unattended scope excludes real manual regression.

## When To Stop And Mark Blocked

Only for real blockers:

- the main plan, progress doc, and handover cannot be reconciled
- another active executor owns the loop
- state ownership is stale or ambiguous
- an external environment is required for the current task
- verification keeps failing and no smaller local repair path remains
- a destructive migration lacks a rollback definition

Routine human review is not a default blocker.

## Early Stop And Cost Control

Do not hallucinate work when meaningful automated work is exhausted.

Stop immediately when:

- no eligible task exists
- all remaining tasks are blocked, deferred behind unresolved blockers, omitted, manual-only, or require human judgment or external environment
- a selected task is manual-only and outside unattended scope

Treat max-iteration count as a safety ceiling, not a target.

Prefer stopping over low-value looping when:

- consecutive iterations produce no code diff and no task-state advancement
- only omitted or manual-only work remains
- only blocked work remains
- the request is unsuitable for this skill or lacks required prerequisites that only the user can provide

## Exit Signals

At the end of the iteration, emit exactly one of these machine-readable status lines:

- `STATUS: ITERATION_COMPLETE_CONTINUE`
- `STATUS: ALL_AUTOMATABLE_TASKS_DONE`
- `STATUS: NO_ELIGIBLE_TASK`
- `STATUS: BLOCKED_NEEDS_HUMAN`

Use them precisely:

- `ITERATION_COMPLETE_CONTINUE` when this iteration safely closed or advanced the selected task and another eligible automated task still exists after re-reading on-disk state
- `ALL_AUTOMATABLE_TASKS_DONE` when all unattended executable tasks are closed and only omitted manual-only work may remain
- `NO_ELIGIBLE_TASK` when there is no eligible automated task right now, but this is not a hard blocker
- `BLOCKED_NEEDS_HUMAN` when ambiguity, ownership conflict, external dependency, or unrecoverable verification failure requires human action

Do not output `ALL_AUTOMATABLE_TASKS_DONE` if meaningful automatable blocked work still exists.

The `STATUS:` line must appear alone on a single line, exactly in one of these forms:

```text
STATUS: ITERATION_COMPLETE_CONTINUE
STATUS: ALL_AUTOMATABLE_TASKS_DONE
STATUS: NO_ELIGIBLE_TASK
STATUS: BLOCKED_NEEDS_HUMAN
```

## Hard Constraints

- never revert unrelated existing user changes
- never revert unowned workspace modifications
- never expand the App -> Interop allowlist
- never reintroduce `CTOOLKIT_USE_APPLICATION_*` as a new rollback mechanism
- never claim completion without verification evidence
- never treat omitted manual regression as completed automated work
- never use governing-doc edits to hide execution truth

## Required Outputs

Before the iteration ends, ensure:

- `.codex/refactor-state.json` is updated when state changed
- `docs/refactor/tasks.json` is updated when the execution graph changed
- required docs are updated when execution truth changed
- `EXECUTION_PLAN` was emitted before file edits
- `RESULT_SUMMARY` was emitted before exit
- exactly one `STATUS:` line was emitted

If there is no real blocker, prefer fully closing one task over partial progress on many tasks.
