# ClassroomToolkit Autonomous Refactor Loop Design

Last updated: 2026-03-11  
Status: active

## 1. Goal

- Let Codex CLI continue the target-architecture refactor instead of stopping after two or three tasks.
- Convert the current human-facing architecture docs into a machine-readable task source and state machine.
- Provide a recoverable, resumable, auditable loop without disturbing the existing dirty worktree.

## 2. Problem

The repo already has strong decision docs:

- `docs/plans/2026-03-06-best-target-architecture-plan.md`
- `docs/validation/2026-03-06-target-architecture-progress.md`
- `docs/handover.md`
- `docs/validation/target-architecture-final-acceptance.md`

But they are not enough for long-running autonomous execution. Missing pieces:

- A machine-readable task backlog
- A persistent state file
- Clear task closure criteria and verification commands
- A supervisor that can restart the loop after a session ends

## 3. Options

### Option A: Prompt-only workflow

Pros:

- Fastest to start
- No extra files to maintain

Cons:

- Depends on single-session context
- Weak resume behavior
- No audit trail for why the loop stopped

Decision: reject.

### Option B: Build a generic cross-project skill first

Pros:

- Better theoretical reuse

Cons:

- Higher abstraction cost
- Slows down the current ClassroomToolkit refactor
- Weakens project-specific verification rules

Decision: reject for now.

### Option C: Repo-local autonomous refactor loop

Pros:

- Reuses the current target-architecture docs directly
- Solves the immediate stop-and-wait problem fastest
- Can be generalized later after it works here

Cons:

- Initial version is project-specific

Decision: adopt.

## 4. Chosen Architecture

### 4.1 Main Components

Add these repo-local components:

- Skill: `.codex/skills/autonomous-refactor-loop/`
- Task source: `docs/refactor/tasks.json`
- State file: `.codex/refactor-state.json`
- Helper scripts:
  - `scripts/refactor/select-next-task.ps1`
  - `scripts/refactor/update-refactor-state.ps1`
  - `scripts/refactor/check-task-done.ps1`
- Supervisor:
  - `scripts/run-refactor-loop.ps1`

### 4.2 Design Principles

- Optimize for this repo first
- Persist execution state on disk
- Use JSON instead of YAML to avoid extra parser dependencies
- Let the skill own one iteration; let the supervisor own retries and continuation
- Never revert unrelated existing user changes

## 5. Responsibilities

### 5.1 Skill

The skill must:

- Read the governing docs, task source, and state file
- Decide whether the selected task is already done
- Implement the smallest safe closure path when needed
- Run task-specific verification commands
- Update the state file
- Split an oversized task into smaller child tasks when necessary

### 5.2 Supervisor

The supervisor must:

- Select the next ready task repeatedly
- Call Codex explicitly for one autonomous iteration
- Reload state after each iteration
- Stop when work is complete, blocked, or not making progress

### 5.3 Task Source

The task source must define:

- Order and priority
- Dependencies
- File hints
- `done_when` criteria
- `verify.commands`
- Docs that need syncing

### 5.4 State File

The state file must record:

- Current task
- Per-task status and attempt count
- Blockers
- Execution history

## 6. State Machine

### 6.1 Allowed Task States

- `pending`
- `in_progress`
- `completed`
- `blocked`
- `deferred`

### 6.2 Transitions

- `pending -> in_progress`
- `in_progress -> completed`
- `in_progress -> blocked`
- `in_progress -> deferred`
- `blocked -> pending`
- `deferred -> pending`

### 6.3 Selection Rules

A task is ready when:

- It is not `completed`
- It is not `blocked` or `deferred`
- All dependencies are completed
- It does not require an external manual gate right now

Sort order:

- First by `priority`
- Then by `order`

## 7. Task Completion

Each task must contain:

- `summary`
- `depends_on`
- `file_hints`
- `done_when`
- `verify.commands`

A task is complete only when all of these are true:

- The implementation satisfies `done_when`
- Verification commands pass
- State is updated with a result summary

## 8. Stop Conditions

The loop stops only when:

- All tasks are completed
- The current task becomes blocked
- State does not move after an iteration
- Codex fails repeatedly without progress
- A manual environment or manual regression gate is required

Waiting for routine human review is not a default stop reason.

## 9. Relationship To Existing Docs

The current decision order stays the same:

- Main plan
- Main progress doc
- Handover
- ADR and history docs

The task source is only an execution map. It must not override the main plan.

## 10. Scope And Non-Goals

### 10.1 Initial Scope

- Get a repo-local autonomous loop working
- Select tasks, update state, and generate the next prompt
- Cover the current remaining target-architecture workstreams

### 10.2 Non-Goals

- No generic cross-project packaging in v1
- No database or remote queue integration in v1
- No fully automated manual regression in v1
- No rewrite of the current main architecture documents in v1

## 11. Rollback

If this loop does not work well, these files can be removed independently without touching the current refactor code:

- `.codex/skills/autonomous-refactor-loop/`
- `.codex/refactor-state.json`
- `docs/refactor/tasks.json`
- `scripts/refactor/*.ps1`
- `scripts/run-refactor-loop.ps1`
- This design doc and the implementation plan

## 12. Usage

Single iteration:

```powershell
codex --cd E:\PythonProject\ClassroomToolkit --ask-for-approval never "Read .codex/skills/autonomous-refactor-loop/SKILL.md and execute one autonomous refactor iteration for ClassroomToolkit."
```

Automatic loop:

```powershell
powershell -File scripts/run-refactor-loop.ps1 -MaxIterations 10
```

## 13. Future Extraction

After this repo-local version proves itself, extract these generic pieces later:

- Task schema
- State machine
- Blocker and resume protocol
- Generic supervisor template
