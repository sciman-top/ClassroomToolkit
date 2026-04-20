# 2026-04-20 Domain RowKey Compatibility Hash Guard

## Current Landing And Target
- boundary: `src/ClassroomToolkit.Domain/Utilities/IdentityUtils.cs`
- current_landing: `IdentityUtils.BuildRowKey`
- target_destination: preserve existing row-key compatibility semantics while making weak-hash usage explicit and analyzer-auditable

## Review Summary
- P1 fixed: `BuildRowKey` used SHA1 without explicit analyzer suppression rationale, making future analyzer expansion ambiguous
- P2 improved: hash computation switched to static `SHA1.HashData` with same algorithm/output, reducing per-call allocation and aligning with analyzer guidance

## Root Cause
- row key generation is a compatibility identifier rather than a security boundary, but this intent was implicit in code and not machine-checkable

## TDD Evidence
- red command:
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~IdentityUtilsTests.BuildRowKey_ShouldDocumentSha1CompatibilitySuppression"`
- red result:
  - 1 failure
  - key evidence: source did not contain `CA5350` suppression marker
- green changes:
  - added `SuppressMessage` on `BuildRowKey` with explicit compatibility justification
  - replaced `SHA1.Create().ComputeHash(...)` with `SHA1.HashData(...)` (same algorithm/output)
  - added source contract test for suppression marker and justification text
- green re-run:
  - targeted tests passed (`通过: 2, 失败: 0`) including deterministic key test

## Files Changed
- `src/ClassroomToolkit.Domain/Utilities/IdentityUtils.cs`
- `tests/ClassroomToolkit.Tests/IdentityUtilsTests.cs`

## Verification
- targeted_tests:
  - command: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~IdentityUtilsTests.BuildRowKey_ShouldDocumentSha1CompatibilitySuppression|FullyQualifiedName~IdentityUtilsTests.BuildRowKey_ShouldBeDeterministic_ForEquivalentInputs"`
  - result: PASS
  - key_output: `通过: 2, 失败: 0`
- domain_analyzer_preflight_ca1850:
  - command: `dotnet build src/ClassroomToolkit.Domain/ClassroomToolkit.Domain.csproj -c Debug -p:EnableNETAnalyzers=true -p:AnalysisLevel=latest-recommended -p:WarningsAsErrors=CA1850`
  - result: PASS
  - key_output: `0 个警告`, `0 个错误`
- domain_analyzer_preflight_ca5350:
  - command: `dotnet build src/ClassroomToolkit.Domain/ClassroomToolkit.Domain.csproj -c Debug -p:EnableNETAnalyzers=true -p:AnalysisLevel=latest-recommended -p:WarningsAsErrors=CA5350`
  - result: PASS
  - key_output: `0 个警告`, `0 个错误`
- build:
  - command: `dotnet build ClassroomToolkit.sln -c Debug`
  - result: PASS
  - key_output: `0 个警告`, `0 个错误`
- test:
  - command: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - result: PASS
  - key_output: `通过: 3361, 失败: 0`
- contract_invariant:
  - command: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - result: PASS
  - key_output: `通过: 28, 失败: 0`
- hotspot:
  - command: `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
  - result: PASS
  - key_output: `[hotspot] PASS - all .cs files within line budget (max=1200)`

## Rollback
- revert:
  - `src/ClassroomToolkit.Domain/Utilities/IdentityUtils.cs`
  - `tests/ClassroomToolkit.Tests/IdentityUtilsTests.cs`
- rerun:
  - targeted identity tests
  - domain CA1850/CA5350 preflight
  - `build -> test -> contract/invariant -> hotspot`
