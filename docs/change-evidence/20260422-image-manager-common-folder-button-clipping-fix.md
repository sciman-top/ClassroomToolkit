# 2026-04-22 Image Manager Common Folder Button Clipping Fix

## Rule Mapping
- R1 boundary: common-folder quick actions layout only.
- Current landing: `ImageManagerWindow.xaml`.
- Target destination: the three quick action buttons render fully in narrow left-panel widths without increasing button count.
- Risk: low. Layout-only XAML change.

## Changes
- Replaced the common-folder quick actions container from a 3-column `Grid` to `UniformGrid Columns="3"`.
- Removed local `MinWidth="86"` overrides from:
  - `添加收藏`
  - `取消收藏`
  - `清空最近`
- Kept the shared toolbar button styles and equal button sizing.
- Preserved button count, order, and click handlers.

## Why It Was Clipping
- The left column minimum width is constrained.
- The old layout combined:
  - `3 x star columns`
  - local `MinWidth="86"`
  - extra right margins
- This exceeded the available inner width, so the second and third buttons were clipped on the right edge.

## Commands And Evidence

### Targeted Test
Command:
```powershell
$out = Join-Path (Resolve-Path .).Path 'artifacts\ctk-image-toolbar-fix-out'
if (Test-Path $out) { Remove-Item -LiteralPath $out -Recurse -Force }
New-Item -ItemType Directory -Path $out | Out-Null
$env:DOTNET_ROLL_FORWARD='LatestMajor'
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ImageManagerTouchFlowContractTests" -p:OutDir=$out\
```

Result:
```text
Passed: 3, Failed: 0, Skipped: 0
```

### Build Gate
Alternative verification command:
```powershell
$out = Join-Path (Resolve-Path .).Path 'artifacts\ctk-image-toolbar-fix-gate'
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
- reason: standard Debug output may be locked by the currently running app / Visual Studio.
- alternative_verification: isolated `OutDir` build under ignored `artifacts/`.
- evidence_link: this file.
- expires_at: rerun standard gates after closing the running app before release.

### Contract / Invariant Gate
Command:
```powershell
$out = Join-Path (Resolve-Path .).Path 'artifacts\ctk-image-toolbar-fix-gate'
$env:DOTNET_ROLL_FORWARD='LatestMajor'
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests" -p:OutDir=$out\ --no-restore
```

Result:
```text
Passed: 28, Failed: 0, Skipped: 0
```

## Hotspot Review
- Buttons still use shared compact toolbar styles.
- All three buttons remain the same size.
- No command handler or favorites/recents logic changed.
- The fix reduces rigid minimum width pressure instead of shrinking the whole left panel.

## Rollback
- Replace the `UniformGrid` with the previous 3-column `Grid`.
- Restore the three local `MinWidth="86"` declarations.
- Remove the added assertions from `ImageManagerTouchFlowContractTests`.
