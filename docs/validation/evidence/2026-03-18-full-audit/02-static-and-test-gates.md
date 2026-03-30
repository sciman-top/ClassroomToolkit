# 02 Static And Test Gates

- Date: 2026-03-18
- Baseline commit: `7c5df30`
- Runner: local Windows PowerShell

## Command Results

1. `dotnet restore ClassroomToolkit.sln --locked-mode`
- Result: PASS
- Key output: 所有项目均是最新的，无法还原。

2. `dotnet list ClassroomToolkit.sln package --vulnerable --include-transitive`
- Result: PASS
- Key output: 所有项目均无易受攻击包。

3. `dotnet build ClassroomToolkit.sln -c Debug --no-restore /warnaserror`
- Result: PASS
- Key output: 0 warnings, 0 errors.

4. `dotnet build ClassroomToolkit.sln -c Release --no-restore /warnaserror`
- Result: PASS
- Key output: 0 warnings, 0 errors.

5. Key contracts gate
- Command:
  `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- Result: PASS
- Summary: Passed 23 / Failed 0.

6. Full Debug tests + coverage
- Command:
  `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --collect:"XPlat Code Coverage" --results-directory artifacts/TestResults/Debug --logger "trx;LogFileName=debug.trx" --blame-hang --blame-hang-timeout 5m`
- Result: PASS
- Summary: Passed 2780 / Failed 0.
- Artifacts:
  - `artifacts/TestResults/Debug/debug.trx`
  - `artifacts/TestResults/Debug/d3bb67ce-44c1-4920-9803-e1ff90424378/coverage.cobertura.xml`

7. Full Release tests
- Command:
  `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Release --no-build --results-directory artifacts/TestResults/Release --logger "trx;LogFileName=release.trx" --blame-hang --blame-hang-timeout 5m`
- Result: PASS
- Summary: Passed 2780 / Failed 0.
- Artifact:
  - `artifacts/TestResults/Release/release.trx`

## Gate Verdict

- Chunk-1 gates: PASS
- Blockers: None
- Recommended next: execute Chunk-2 hotspot deep review and create `03-hotspot-findings.md`.
