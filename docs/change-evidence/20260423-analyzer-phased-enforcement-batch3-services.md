# 20260423 Analyzer Phased Enforcement Batch 3 (Services)

## Goal

Promote `ClassroomToolkit.Services` into analyzer enforced batch and continue lowering `latest-all` backlog without breaking the existing quality gate chain.

## Scope

- `Directory.Build.props`
- `src/ClassroomToolkit.Services/Presentation/PresentationCommandMapper.cs`
- `src/ClassroomToolkit.Services/Input/GlobalHookService.cs`
- `src/ClassroomToolkit.Services/Presentation/PresentationClassifierOverridesPackagePolicy.cs`
- `src/ClassroomToolkit.Services/Compatibility/StartupCompatibilityProbe.cs`
- `scripts/quality/analyzer-backlog-baseline.json`

## Changes

1. Batch extension:
   - Added `ClassroomToolkit.Services` into enforced analyzer project set in `Directory.Build.props`:
     - `EnableNETAnalyzers=true`
     - `AnalysisLevel=latest-recommended`
     - `TreatWarningsAsErrors=true`
2. Services warning closure (recommended level):
   - `PresentationCommandMapper.Map` kept instance API with explicit `CA1822` suppression (DI/test compatibility).
   - `GlobalHookService.CleanupHooks` converted to `static` (CA1822).
   - Reused static `JsonSerializerOptions` in:
     - `PresentationClassifierOverridesPackagePolicy`
     - `StartupCompatibilityReport.ToJson`
     to resolve `CA1869`.
   - Tightened `StartupCompatibilityProbe` method signatures to concrete collection types (`List<>` / `HashSet<>`) to resolve `CA1859`.
3. Backlog ratchet:
   - refreshed `scripts/quality/analyzer-backlog-baseline.json` from latest report.
   - backlog total reduced from `775` to `751`.

## Verification

1. Services enforced build:
   - `dotnet build src/ClassroomToolkit.Services/ClassroomToolkit.Services.csproj -c Debug --no-incremental -m:1 -p:EnableNETAnalyzers=true -p:AnalysisLevel=latest-recommended -p:TreatWarningsAsErrors=true`
   - result: `PASS` (`0 warnings`, `0 errors`)
2. Solution build:
   - `dotnet build ClassroomToolkit.sln -c Debug -m:1`
   - result: `PASS`
3. Full tests:
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1`
   - result: `PASS` (`3425 passed`)
4. Contract/invariant tests:
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - result: `PASS` (`28 passed`)
5. Analyzer backlog guard:
   - `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug`
   - result: `PASS` (`total=751`)
6. Full quality chain:
   - `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile quick -Configuration Debug`
   - result: `ALL PASS`

## Risks

- `App` remains outside enforced analyzer batch and is still controlled by backlog baseline ratchet.
- Local `MSB3026/CS2012` file-lock warnings can occur if multiple compile/test commands run concurrently; current execution switched to serial verification for stability.

## Rollback

- `git restore --source=HEAD~1 -- Directory.Build.props src/ClassroomToolkit.Services/Presentation/PresentationCommandMapper.cs src/ClassroomToolkit.Services/Input/GlobalHookService.cs src/ClassroomToolkit.Services/Presentation/PresentationClassifierOverridesPackagePolicy.cs src/ClassroomToolkit.Services/Compatibility/StartupCompatibilityProbe.cs scripts/quality/analyzer-backlog-baseline.json docs/change-evidence/20260423-analyzer-phased-enforcement-batch3-services.md`
