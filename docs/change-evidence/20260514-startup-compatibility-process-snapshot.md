# 2026-05-14 startup compatibility process snapshot optimization

## Scope

- Rule IDs: R1/R2/R3/R6/R8, E4
- Risk level: low
- Boundary: startup compatibility PPT/WPS process enumeration in `StartupCompatibilityProbe` and its source contract test.
- Goal: reduce startup diagnostics work and volatile process identity reads without changing report issue codes, blocking/warning semantics, persisted settings, `students.xlsx`, `student_photos/`, or `settings.ini`.

## Basis

- Existing optimization plan points to `StartupCompatibilityProbe.cs` as a large, high-value follow-up target.
- Baseline `dotnet build ClassroomToolkit.sln -c Debug`: passed, 0 warnings, 0 errors.
- Static scan found no production `.Result`, `.Wait(`, `.GetAwaiter().GetResult()`, `Thread.Sleep`, `GC.Collect`, `TODO`, `FIXME`, or `HACK` issues in this slice.
- Structural baseline from `HEAD`: presentation process enumeration loop appeared twice; after this change it appears once.

## Change

- Added a private `PresentationProcessSnapshot` for matched PPT/WPS process label and process id.
- Changed startup compatibility collection to enumerate matching presentation processes once, then reuse snapshots for privilege and architecture checks.
- Removed the replaced private label helper path and kept volatile `Process.ProcessName` / `Process.Id` reads centralized.
- Updated `StartupCompatibilityProbeTests` source contract to lock the snapshot path and single enumeration loop.

## Verification

- Focused test:
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~StartupCompatibilityProbeTests"`: passed, 13 passed.
- Hard gate:
  - build: `dotnet build ClassroomToolkit.sln -c Debug`: passed, 0 warnings, 0 errors.
  - test: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`: passed, 3487 passed.
  - contract/invariant: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`: passed, 29 passed.
  - hotspot: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality\check-hotspot-line-budgets.ps1`: passed.
  - whitespace: `git diff --check`: passed with only existing LF/CRLF normalization warnings for touched files.

## Hotspot Review

- Startup presentation checks still produce the same issue codes and severity paths: `presentation-privilege-*` and `presentation-arch-*`.
- Unknown process id handling is preserved as `process-id-unavailable`.
- Interop calls remain guarded through existing non-fatal exception handling and handle cleanup.
- The performance improvement is bounded and structural: one `Process.GetProcesses()` matching pass instead of two for the PPT/WPS startup compatibility path.

## Rollback

```powershell
git restore -- src/ClassroomToolkit.Services/Compatibility/StartupCompatibilityProbe.cs tests/ClassroomToolkit.Tests/StartupCompatibilityProbeTests.cs docs/change-evidence/20260514-startup-compatibility-process-snapshot.md
```
