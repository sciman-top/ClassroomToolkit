# 2026-04-20 README Cleanup And Build Hardening

## Current Landing And Target
- boundary: repository root documentation and build configuration
- current_landing: `README.md`, `README.en.md`, `Directory.Build.props`, `tests/ClassroomToolkit.Tests/RepositoryDocumentationAndBuildHardeningContractTests.cs`
- target_destination: remove stale repository references from public docs and add a low-risk repository-wide build hardening baseline without changing runtime behavior

## Review Summary
- P1 fixed: both README files linked to a missing runbook path `docs/runbooks/release-prevention-checklist.md`
- P1 fixed: `README.md` contained unrelated governance onboarding content and a missing `scripts/doctor.ps1` command
- P1 fixed: `README.en.md` contained a missing `tools/browser-session/start-browser-session.ps1` helper example
- P2 fixed: repository had no centralized build rule to fail fast on future warnings
- deferred: enabling additional analyzers / `AnalysisLevel=latest-recommended` was preflighted and intentionally not landed because it surfaced many existing CA violations across `Domain` and `Interop`, which would exceed the scope of this batch

## Root Cause
- README drift: copied or retained documentation sections outlived the files/scripts they referenced
- build hardening gap: warning policy was only implicit in local command usage and not enforced centrally

## TDD Evidence
- red command:
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~RepositoryDocumentationAndBuildHardeningContractTests"`
- red result:
  - 5 failures
  - missing markdown link evidence: `./docs/runbooks/release-prevention-checklist.md`
  - missing script evidence: `scripts/doctor.ps1`, `tools/browser-session/start-browser-session.ps1`
  - missing hardening evidence: `Directory.Build.props` not found
- green changes:
  - added repository contract tests for README link/script validity and centralized warning policy
  - corrected both README files to existing runbook paths and removed missing-script examples
  - added `Directory.Build.props` with `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
- green re-run:
  - same targeted command passed with `通过: 5, 失败: 0`

## Files Changed
- `README.md`
- `README.en.md`
- `Directory.Build.props`
- `tests/ClassroomToolkit.Tests/RepositoryDocumentationAndBuildHardeningContractTests.cs`

## Verification
- preflight-safe hardening:
  - command: `dotnet build ClassroomToolkit.sln -c Debug -p:TreatWarningsAsErrors=true`
  - result: PASS
  - key_output: `0 个警告`, `0 个错误`
- preflight-deferred hardening:
  - command: `dotnet build ClassroomToolkit.sln -c Debug -p:EnableNETAnalyzers=true -p:AnalysisLevel=latest-recommended -p:TreatWarningsAsErrors=true`
  - result: FAIL
  - key_output: existing CA diagnostics surfaced across `ClassroomToolkit.Domain` and `ClassroomToolkit.Interop` (examples: `CA1859`, `CA5350`, `CA1401`, `CA2101`)
  - decision: do not land analyzer-expansion in this batch
- build:
  - command: `dotnet build ClassroomToolkit.sln -c Debug`
  - result: PASS
  - key_output: `0 个警告`, `0 个错误`
- test:
  - command: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - result: PASS
  - key_output: `通过: 3355, 失败: 0`
- contract_invariant:
  - command: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - result: PASS
  - key_output: `通过: 28, 失败: 0`
- hotspot:
  - command: `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
  - result: PASS
  - key_output: `[hotspot] PASS - all .cs files within line budget (max=1200)`
- hotspot_manual_review:
  - file: `README.md`
  - conclusion: removed unrelated governance-onboarding block and corrected link to existing runbook only
  - file: `README.en.md`
  - conclusion: removed missing browser helper example and corrected link to existing runbook only
  - file: `Directory.Build.props`
  - conclusion: central policy limited to warnings-as-errors; no analyzer expansion landed
  - file: `tests/ClassroomToolkit.Tests/RepositoryDocumentationAndBuildHardeningContractTests.cs`
  - conclusion: guards future README drift and absence of central warning policy

## Rollback
- remove `Directory.Build.props`
- revert changes in:
  - `README.md`
  - `README.en.md`
  - `tests/ClassroomToolkit.Tests/RepositoryDocumentationAndBuildHardeningContractTests.cs`
- rerun:
  - `dotnet build ClassroomToolkit.sln -c Debug`
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - contract/invariant filter
  - hotspot script
