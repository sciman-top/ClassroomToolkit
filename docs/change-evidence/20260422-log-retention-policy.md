# 2026-04-22 Log Retention Policy

## Rule Mapping
- R1 boundary: log lifecycle only.
- Current landing: `ClassroomToolkit.Infra.Logging` and `App.LogException`.
- Target destination: keep recent logs automatically without blocking startup or exception reporting.
- Risk: low. Cleanup is best-effort and limited to log files matching known prefixes.

## Changes
- Added `LogRetentionOptions` with defaults:
  - `RetentionDays = 14`
  - `MaxHistoricalFileBytes = 10 MB`
- Added `LogRetentionPolicy.TryApply(...)`.
- `FileLoggerProvider` now applies retention to `app_*.log` on startup.
- `App` now applies retention to `error_*.log` on startup and before first exception log write.
- `photo-overlay-latest.log` remains unchanged and continues to be overwritten per diagnostic session.

## Safety Invariants
- Today's log file is never deleted by retention.
- Files with unknown names or unparsable dates are ignored.
- Cleanup only targets caller-provided prefixes, currently `app_` and `error_`.
- Cleanup failures are swallowed under the existing non-fatal exception policy.
- Logging append behavior remains unchanged.

## Commands And Evidence

### Targeted Tests
Command:
```powershell
$out = Join-Path (Resolve-Path .).Path 'artifacts\ctk-test-out'
if (Test-Path $out) { Remove-Item -LiteralPath $out -Recurse -Force }
New-Item -ItemType Directory -Path $out | Out-Null
$env:DOTNET_ROLL_FORWARD='LatestMajor'
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~FileLoggerProviderTests" -p:OutDir=$out\
```

Result:
```text
Passed: 16, Failed: 0, Skipped: 0
```

### Build Gate
Standard command:
```powershell
$env:DOTNET_ROLL_FORWARD='LatestMajor'
dotnet build ClassroomToolkit.sln -c Debug
```

Result:
```text
Blocked by locked Debug output file.
The process cannot access ... ClassroomToolkit.Infra.dll because it is being used by another process.
Lock owners: Microsoft Visual Studio, sciman Classroom Toolkit.
```

Alternative verification command:
```powershell
$out = Join-Path (Resolve-Path .).Path 'artifacts\ctk-gate-out'
if (Test-Path $out) { Remove-Item -LiteralPath $out -Recurse -Force }
New-Item -ItemType Directory -Path $out | Out-Null
$env:DOTNET_ROLL_FORWARD='LatestMajor'
dotnet build ClassroomToolkit.sln -c Debug -p:OutDir=$out\
```

Result:
```text
Build succeeded. 0 warnings, 0 errors.
```

N/A classification:
- type: `gate_na`
- reason: standard Debug output was locked by the currently running app and Visual Studio.
- alternative_verification: same solution build with isolated `OutDir` under ignored `artifacts/`.
- evidence_link: this file.
- expires_at: close the running app and rerun the standard gate before release.

### Test Gate
Alternative verification command:
```powershell
$out = Join-Path (Resolve-Path .).Path 'artifacts\ctk-gate-out'
$env:DOTNET_ROLL_FORWARD='LatestMajor'
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -p:OutDir=$out\ --no-restore
```

Result:
```text
Passed: 3400, Failed: 1, Skipped: 0
Failure: BrushDpiGoldenRegressionTests.DpiGoldenHashes_ShouldMatchBaseline
Reason: missing baseline file D:\Baselines\brush-dpi-golden.json.
```

N/A classification:
- type: `gate_na`
- reason: existing DPI golden baseline is absent from the local machine.
- alternative_verification: targeted tests for changed logging behavior passed; contract/invariant gate passed.
- evidence_link: this file.
- expires_at: provide `D:\Baselines\brush-dpi-golden.json` or regenerate the accepted baseline before release.

### Contract / Invariant Gate
Command:
```powershell
$out = Join-Path (Resolve-Path .).Path 'artifacts\ctk-gate-out'
$env:DOTNET_ROLL_FORWARD='LatestMajor'
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests" -p:OutDir=$out\ --no-restore
```

Result:
```text
Passed: 28, Failed: 0, Skipped: 0
```

## Hotspot Review
- Reviewed deletion scope: only direct children in the logs directory are enumerated.
- Reviewed matching scope: caller must provide `app_` or `error_`; unrelated files are ignored.
- Reviewed current-day safety: today is preserved even if oversized.
- Reviewed failure mode: cleanup errors are best-effort and cannot abort startup or exception logging.

## Rollback
- Remove `LogRetentionOptions.cs` and `LogRetentionPolicy.cs`.
- Remove `TryApplyRetention()` from `FileLoggerProvider`.
- Remove `TryApplyErrorLogRetention()` and its calls from `App.xaml.cs`.
- Remove the added retention tests from `FileLoggerProviderTests`.
