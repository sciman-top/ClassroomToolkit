# 2026-04-24 Systematic Review And Optimization

## Scope
- Rule: R1-R8, C.2 fixed gate order.
- Boundary: low-risk correctness and quality-gate review in `D:\CODE\ClassroomToolkit`.
- Current location: `src/ClassroomToolkit.Domain/Utilities/AtomicFileReplaceUtility.cs`.
- Target home: preserve public behavior while ensuring fallback replace cleanup matches the atomic write contract.

## Baseline
- `git status --short --branch`: clean `main...origin/main`.
- `dotnet build ClassroomToolkit.sln -c Debug`: failed with `0 warning / 0 error`; diagnostic log showed `_GetProjectReferenceTargetFrameworkProperties` failure without source errors.
- `dotnet build src/ClassroomToolkit.App/ClassroomToolkit.App.csproj -c Debug --no-restore -m:1 -v:minimal /p:UseSharedCompilation=false`: passed.
- `dotnet build ClassroomToolkit.sln -c Debug --no-restore -m:1 -v:minimal /p:UseSharedCompilation=false`: blocked by generated test output ACL, `MSB3491 Access to the path is denied` for `.msCoverageSourceRootsMapping_ClassroomToolkit.Tests`.
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --no-build -v:minimal`: blocked by `SocketException (10106)` while VSTest initialized its local socket channel.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`: passed.

## Change
- `AtomicFileReplaceUtility.ReplaceOrOverwrite` now deletes the temp file after the fallback copy succeeds.

## Post-change Verification
- `dotnet build src/ClassroomToolkit.Domain/ClassroomToolkit.Domain.csproj -c Debug --no-restore -m:1 -v:minimal /p:UseSharedCompilation=false`: passed with `0 warning / 0 error`.
- `dotnet build src/ClassroomToolkit.Application/ClassroomToolkit.Application.csproj -c Debug --no-restore -m:1 -v:minimal /p:UseSharedCompilation=false`: passed with `1 warning / 0 error`; warning was generated `obj` cache ACL, `MSB3101 Access to the path is denied`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`: passed.
- `dotnet build src/ClassroomToolkit.App/ClassroomToolkit.App.csproj -c Debug --no-restore -m:1 -v:minimal /p:UseSharedCompilation=false`: blocked by generated `obj` ACL in WPF `MarkupCompilePass1`, `MC1000/MSB4018 Access to the path ... App.g.cs/sciman Classroom Toolkit_MarkupCompile.cache is denied`.

## Risk
- Low. The normal `File.Replace` path is unchanged.
- Fallback already means the target was overwritten successfully; deleting the temp file aligns fallback behavior with the normal path and existing cleanup expectations.

## Rollback
- Revert the one-line `File.Delete(tempPath)` addition in `src/ClassroomToolkit.Domain/Utilities/AtomicFileReplaceUtility.cs`.

## N/A / Blockers
- `platform_na`: `codex --version`, `codex --help`, and `codex status` failed in this environment with Node native `ncrypto::CSPRNG` assertion.
- `gate_na`: full `dotnet test` could not run because VSTest local socket initialization failed with `SocketException (10106)`.
- `gate_na`: solution/test-project build was blocked by generated output ACL preventing MSBuild `WriteLinesToFile` from replacing coverage mapping files.
- Alternative verification: app/project build with single MSBuild node and hotspot line-budget gate.
- Evidence link: `artifacts/validation/baseline-build-20260424.log`, `artifacts/validation/baseline-app-build-20260424.log`, this file.
- Expires at: rerun after workspace ACL and local socket provider are repaired.
