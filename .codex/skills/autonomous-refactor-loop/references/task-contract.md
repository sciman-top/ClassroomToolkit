# Task Contract

The execution graph in `docs/refactor/tasks.json` is mutable. The governing architecture docs are not.

Each task object should contain at least these fields:

```json
{
  "id": "overlay-input-tail",
  "title": "Close the remaining Overlay input tail work",
  "stage": "runtime-tail",
  "parent_id": "overlay-tail-stage",
  "priority": 1,
  "order": 120,
  "depends_on": ["overlay-presentation-tail"],
  "status_hint": "active",
  "summary": "Short task summary",
  "file_hints": ["src/ClassroomToolkit.App/Paint/"],
  "done_when": ["Closure rule 1", "Closure rule 2"],
  "verify": {
    "commands": ["dotnet test ..."]
  },
  "doc_sync": ["docs/handover.md"],
  "mutation": {
    "allow_split": true,
    "allow_reorder_within_stage": true
  }
}
```

## Field Definitions

- `id`: stable unique identifier
- `title`: human-readable title
- `stage`: high-level execution stage
- `parent_id`: optional parent node id
- `priority`: lower number means higher priority
- `order`: secondary order within the same priority
- `depends_on`: list of task ids that must be completed first
- `status_hint`: one of `completed`, `active`, `blocked-later`, or `omitted`
- `summary`: smallest meaningful task goal
- `file_hints`: main files or directories to inspect
- `done_when`: closure criteria
- `verify.commands`: required verification commands
- `doc_sync`: docs that must be updated when execution truth changes
- `manual_gate`: true only for real manual-only tasks
- `mutation.allow_split`: whether the skill may split this task
- `mutation.allow_reorder_within_stage`: whether the skill may adjust order inside the same stage

## State File Contract

`.codex/refactor-state.json` stores runtime state:

```json
{
  "current_task": "overlay-input-tail",
  "tasks": {
    "overlay-input-tail": {
      "status": "in_progress",
      "attempts": 2,
      "last_summary": "Extracted part of the input routing tail"
    }
  },
  "blocked": [],
  "history": []
}
```

## Allowed Status Values In State

- `pending`
- `in_progress`
- `completed`
- `blocked`
- `deferred`
- `omitted`

## Execution-Graph Mutation Rules

Allowed:

1. split a large active task into child tasks
2. reorder tasks inside the same stage when dependencies remain valid
3. mark a manual-only task as `omitted` when the unattended scope excludes it
4. add a small follow-up tail task after closing a larger task

Not allowed:

1. changing governing architecture truth
2. deleting a blocker without recording it
3. removing dependencies just to force execution
4. marking manual-only work as automated-complete

## Blocker Record Format

Recommended shape:

```json
{
  "task_id": "manual-final-regression",
  "reason": "Needs a real PPT/WPS fullscreen regression environment",
  "recorded_at": "2026-03-11T12:00:00Z"
}
```
