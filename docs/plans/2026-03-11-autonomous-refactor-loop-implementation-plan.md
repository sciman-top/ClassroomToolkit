# Autonomous Refactor Loop Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a repo-local autonomous refactor loop for ClassroomToolkit so Codex can keep selecting tasks, running verification, updating state, and continuing through a supervisor script.  

**Architecture:** Use a repo-local skill, a JSON task source, a JSON state file, and PowerShell scripts. The skill owns one execution iteration. The supervisor owns repeated invocation and continuation.  

**Tech Stack:** PowerShell 7+, Codex CLI, JSON, repo-local SKILL.md

---

### Task 1: Create Design And Planning Docs

**Files:**
- Create: `docs/plans/2026-03-11-autonomous-refactor-loop-design.md`
- Create: `docs/plans/2026-03-11-autonomous-refactor-loop-implementation-plan.md`

**Step 1: Write the design doc**

Cover:

- Problem statement
- Option comparison
- Component responsibilities
- State machine
- Stop conditions
- Rollback strategy

**Step 2: Verify the docs exist**

Run:

```powershell
Get-Item docs/plans/2026-03-11-autonomous-refactor-loop-design.md, docs/plans/2026-03-11-autonomous-refactor-loop-implementation-plan.md
```

Expected: both files exist.

### Task 2: Create Machine-Readable Task Source

**Files:**
- Create: `docs/refactor/tasks.json`
- Create: `.codex/refactor-state.json`

**Step 1: Define the JSON task schema**

Each task must include:

- `id`
- `title`
- `priority`
- `order`
- `depends_on`
- `file_hints`
- `done_when`
- `verify.commands`

**Step 2: Seed the initial backlog**

Map the current remaining target-architecture workstreams into the JSON task list.

**Step 3: Create the initial state file**

Initialize:

- `current_task`
- `tasks`
- `blocked`
- `history`

**Step 4: Verify JSON parses**

Run:

```powershell
Get-Content -Raw docs/refactor/tasks.json | ConvertFrom-Json | Out-Null
Get-Content -Raw .codex/refactor-state.json | ConvertFrom-Json | Out-Null
```

Expected: no error.

### Task 3: Create The Repo-Local Skill

**Files:**
- Create: `.codex/skills/autonomous-refactor-loop/SKILL.md`
- Create: `.codex/skills/autonomous-refactor-loop/references/task-contract.md`

**Step 1: Write SKILL.md**

It must define:

- Required input docs
- State update protocol
- Task split rules
- Blocked rules

**Step 2: Write the task contract reference**

Include field definitions, allowed states, and examples.

**Step 3: Verify skill files exist**

Run:

```powershell
Get-Item .codex/skills/autonomous-refactor-loop/SKILL.md, .codex/skills/autonomous-refactor-loop/references/task-contract.md
```

Expected: both files exist.

### Task 4: Implement PowerShell Helpers

**Files:**
- Create: `scripts/refactor/select-next-task.ps1`
- Create: `scripts/refactor/update-refactor-state.ps1`
- Create: `scripts/refactor/check-task-done.ps1`

**Step 1: Implement task selection**

Rules:

- Skip `completed`, `blocked`, `deferred`, and `in_progress`
- Select only tasks with completed dependencies
- Sort by `priority`, then `order`

**Step 2: Implement state updates**

At minimum support:

- `init`
- `start`
- `complete`
- `block`
- `defer`
- `note`

**Step 3: Implement done-check rendering**

Output:

- task summary
- `done_when` checklist
- `verify.commands`

**Step 4: Verify helper scripts**

Run:

```powershell
powershell -File scripts/refactor/select-next-task.ps1 -AsJson
powershell -File scripts/refactor/check-task-done.ps1 -TaskId reconcile-current-progress
```

Expected:

- selector returns one JSON object or a terminal status object
- checker prints the task checklist

### Task 5: Implement The Supervisor

**Files:**
- Create: `scripts/run-refactor-loop.ps1`

**Step 1: Implement dry-run mode**

Dry-run must:

- select the next task
- print the generated prompt
- avoid calling Codex

**Step 2: Implement live loop mode**

Live mode must:

- call Codex CLI
- point explicitly to the repo-local skill
- reload state after each iteration
- stop if state does not change

**Step 3: Verify dry-run**

Run:

```powershell
powershell -File scripts/run-refactor-loop.ps1 -MaxIterations 1 -DryRun
```

Expected: prints the selected task and prompt preview.

### Task 6: Final Validation

**Files:**
- Verify only

**Step 1: Re-run validation**

Run:

```powershell
Get-Content -Raw docs/refactor/tasks.json | ConvertFrom-Json | Out-Null
Get-Content -Raw .codex/refactor-state.json | ConvertFrom-Json | Out-Null
powershell -File scripts/refactor/select-next-task.ps1 -AsJson
powershell -File scripts/refactor/check-task-done.ps1 -TaskId reconcile-current-progress
powershell -File scripts/run-refactor-loop.ps1 -MaxIterations 1 -DryRun
```

Expected: all commands succeed.

**Step 2: Record usage guidance**

Document:

- how to run one manual iteration
- how to start the automatic loop
- when the loop stops
- how to clear a blocked task
