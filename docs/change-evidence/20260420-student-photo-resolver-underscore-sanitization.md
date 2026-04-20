# 2026-04-20 StudentPhotoResolver Underscore Sanitization

## Current Landing And Target
- boundary: `src/ClassroomToolkit.App/Photos/StudentPhotoResolver.cs`
- current_landing: keep photo path resolution inside existing class-directory lookup and filename sanitization flow
- target_destination: fix false-negative path resolution for valid class names and student IDs that legitimately start or end with `_`, without changing file formats, config semantics, or traversal guards

## Review Summary
- P1 fixed: `StudentPhotoResolver.SanitizeSegment` stripped valid leading/trailing underscores from class names and student IDs, causing real photo files such as `__ClassA__\_1001_.jpg` to become unreachable
- P2 observed, not changed: project references stay stable, build/test gates are healthy, hotspot budget script passes, no evidence-supported dependency or contract break was found during this slice

## Root Cause
- the sanitizer used `Trim('_')` in both the no-sanitize and sanitize branches
- `_` is a valid Windows file-name character, so trimming it changed legitimate identifiers instead of only neutralizing path separators / invalid characters

## TDD Evidence
- red command:
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~StudentPhotoResolverTests.SanitizeSegment_ShouldPreserveValidUnderscores|FullyQualifiedName~StudentPhotoResolverTests.ResolvePhotoPath_ShouldResolve_WhenClassNameAndStudentIdUseValidEdgeUnderscores"`
- red result:
  - 5 failures
  - representative evidence: expected `_1001_`, actual `1001`
  - representative evidence: expected photo path under `__ClassA__\_1001_.jpg`, actual `<null>`
- green change:
  - removed underscore trimming while preserving `.` / `..` rejection and invalid-character replacement
- green re-run:
  - same targeted command passed with `通过: 5, 失败: 0`

## Files Changed
- `src/ClassroomToolkit.App/Photos/StudentPhotoResolver.cs`
- `tests/ClassroomToolkit.Tests/StudentPhotoResolverTests.cs`

## Verification
- build:
  - command: `dotnet build ClassroomToolkit.sln -c Debug`
  - result: PASS
  - key_output: `0 个警告`, `0 个错误`
- test:
  - command: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - result: PASS
  - key_output: `通过: 3350, 失败: 0`
- contract_invariant:
  - command: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - result: PASS
  - key_output: `通过: 28, 失败: 0`
- hotspot:
  - command: `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
  - result: PASS
  - key_output: `[hotspot] PASS - all .cs files within line budget (max=1200)`
- hotspot_manual_review:
  - file: `src/ClassroomToolkit.App/Photos/StudentPhotoResolver.cs`
  - conclusion: changed hunk only removes data-destructive underscore trimming; traversal guard and invalid-character replacement remain intact
  - file: `tests/ClassroomToolkit.Tests/StudentPhotoResolverTests.cs`
  - conclusion: added regression coverage for sanitizer behavior and end-to-end file resolution using underscore-prefixed/suffixed names

## Platform N/A
- type: `platform_na`
- item: `codex status`
- reason: non-interactive shell returned `stdin is not a terminal`
- alternative_verification:
  - `codex --version`
  - `codex --help`
  - repo-local rule source verified from repository `AGENTS.md`
- evidence_link: this file
- expires_at: `2026-04-27`

## Rollback
- revert the two changed hunks in:
  - `src/ClassroomToolkit.App/Photos/StudentPhotoResolver.cs`
  - `tests/ClassroomToolkit.Tests/StudentPhotoResolverTests.cs`
- behavior rollback effect:
  - restores previous underscore-trimming behavior
  - removes the added regression coverage for underscore-preserving resolution
