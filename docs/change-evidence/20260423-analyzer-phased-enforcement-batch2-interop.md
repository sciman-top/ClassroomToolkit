# 20260423 Analyzer Phased Enforcement Batch 2 (Interop)

## Goal

Promote `ClassroomToolkit.Interop` into analyzer enforced batch while keeping the repository gate chain fully green.

## Scope

- `Directory.Build.props`
- `src/ClassroomToolkit.Interop/Win32/NativeMethods.cs`
- `src/ClassroomToolkit.Interop/Presentation/Win32PresentationResolver.Native.cs`
- `src/ClassroomToolkit.Interop/Presentation/PresentationClassifierOverrides.cs`
- `src/ClassroomToolkit.Interop/Presentation/PresentationClassifierAutoLearnPolicy.cs`
- `src/ClassroomToolkit.Interop/Presentation/WpsSlideshowNavigationHook.cs`
- `src/ClassroomToolkit.Interop/Properties/InternalsVisibleTo.cs`
- `scripts/quality/analyzer-backlog-baseline.json`

## Changes

1. Analyzer batch extension:
   - Added `ClassroomToolkit.Interop` to enforced set in `Directory.Build.props`:
     - `EnableNETAnalyzers=true`
     - `AnalysisLevel=latest-recommended`
     - `TreatWarningsAsErrors=true`
2. Interop warning closure for recommended level:
   - `NativeMethods`:
     - replaced `StringBuilder` P/Invoke buffers with `char[]` for `GetClassName` / `GetWindowText` (`CA1838`);
     - applied local `#pragma warning disable/restore CA1401` around P/Invoke declarations to explicitly document boundary intent while preserving current public API surface.
   - `Win32PresentationResolver.Native`:
     - adapted to `char[]` class-name extraction;
     - adjusted method signatures to concrete `List<string>` where analyzer requested (`CA1859`).
   - `PresentationClassifierOverrides` / `PresentationClassifierAutoLearnPolicy`:
     - tightened signatures to concrete collection types (`CA1859`).
   - `WpsSlideshowNavigationHook.Available`:
     - kept instance contract and added explicit `CA1822` suppression with justification.
3. Backlog ratchet:
   - updated `scripts/quality/analyzer-backlog-baseline.json` from latest report after Batch 2.
   - backlog total reduced from `856` to `775`.

## Verification

1. Interop enforced build:
   - `dotnet build src/ClassroomToolkit.Interop/ClassroomToolkit.Interop.csproj -c Debug --no-incremental -m:1 -p:EnableNETAnalyzers=true -p:AnalysisLevel=latest-recommended -p:TreatWarningsAsErrors=true`
   - result: `PASS` (`0 warnings`, `0 errors`)
2. Solution build:
   - `dotnet build ClassroomToolkit.sln -c Debug -m:1`
   - result: `PASS`
3. Full tests:
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
   - result: `PASS` (`3425 passed`)
4. Contract/invariant tests:
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - result: `PASS` (`28 passed`)
5. Analyzer backlog guard:
   - `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug`
   - result: `PASS` (`total=775`)
6. Full quality chain:
   - `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile quick -Configuration Debug`
   - result: `ALL PASS`

## Risks

- `CA1401` in Interop is now explicitly suppressed at Win32 boundary declarations. This is intentional to keep current public access patterns; future boundary redesign can replace it with narrower exposure.
- Local file-lock warnings (`MSB3026`) may still appear transiently when build/test hosts hold binaries.

## Rollback

- `git restore --source=HEAD~1 -- Directory.Build.props src/ClassroomToolkit.Interop/Win32/NativeMethods.cs src/ClassroomToolkit.Interop/Presentation/Win32PresentationResolver.Native.cs src/ClassroomToolkit.Interop/Presentation/PresentationClassifierOverrides.cs src/ClassroomToolkit.Interop/Presentation/PresentationClassifierAutoLearnPolicy.cs src/ClassroomToolkit.Interop/Presentation/WpsSlideshowNavigationHook.cs src/ClassroomToolkit.Interop/Properties/InternalsVisibleTo.cs scripts/quality/analyzer-backlog-baseline.json docs/change-evidence/20260423-analyzer-phased-enforcement-batch2-interop.md`
