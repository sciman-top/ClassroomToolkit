# 2026-04-22 Roll Call Group Button Compacting

## Rule Mapping
- R1 boundary: roll-call bottom bar group button visual density only.
- Current landing: `RollCallWindow.xaml`.
- Target destination: group buttons are narrower while keeping the same count and touch-safe height.
- Risk: low. XAML style-only change; no roll-call logic changed.

## Changes
- Added `Style_RollCallGroupButton`.
- Reduced group button `MinWidth` from the shared bottom bar default `60` to `48`.
- Reduced group button horizontal margin from `3` to `2`.
- Kept button height at `32` through the inherited bottom bar style.
- Kept reset/list/timer buttons unchanged.

## Commands And Evidence

### Targeted Test
Command:
```powershell
$out = Join-Path (Resolve-Path .).Path 'artifacts\ctk-rollcall-compact-out'
if (Test-Path $out) { Remove-Item -LiteralPath $out -Recurse -Force }
New-Item -ItemType Directory -Path $out | Out-Null
$env:DOTNET_ROLL_FORWARD='LatestMajor'
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~DialogTouchFlowContractTests" -p:OutDir=$out\
```

Result:
```text
Passed: 4, Failed: 0, Skipped: 0
```

### Build Gate
Alternative verification command:
```powershell
$out = Join-Path (Resolve-Path .).Path 'artifacts\ctk-rollcall-compact-gate'
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
- reason: standard Debug output has recently been locked by the running app and Visual Studio.
- alternative_verification: same solution build with isolated `OutDir` under ignored `artifacts/`.
- evidence_link: this file.
- expires_at: close the running app and rerun standard gates before release.

### Contract / Invariant Gate
Command:
```powershell
$out = Join-Path (Resolve-Path .).Path 'artifacts\ctk-rollcall-compact-gate'
$env:DOTNET_ROLL_FORWARD='LatestMajor'
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests" -p:OutDir=$out\ --no-restore
```

Result:
```text
Passed: 28, Failed: 0, Skipped: 0
```

## Hotspot Review
- Button count unchanged.
- Group buttons remain uniform with one shared group-specific style.
- Group button height remains inherited from the bottom bar style.
- No roll-call command handlers or data bindings changed.

## Rollback
- Remove `Style_RollCallGroupButton`.
- Change the group button inline style base back to `Style_RollCallBottomBarTextButton`.
- Remove the added style assertions from `DialogTouchFlowContractTests`.
