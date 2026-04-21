# 20260422 ImageManager ScrollViewer Negative Size Hotfix

- date: 2026-04-22
- issue_id: image-manager-scrollviewer-negative-size
- risk_level: medium
- active_rule_path: D:\CODE\ClassroomToolkit\AGENTS.md
- current_boundary: fix Dispatcher.UnhandledException caused by ScrollViewer receiving negative layout dimensions from the thumbnail virtualization path
- target_destination: keep the thumbnail virtualizing panel while guaranteeing non-negative IScrollInfo dimensions
- clarification_mode: direct_fix
- attempt_count: 1

## Basis
- user reported `Dispatcher.UnhandledException`: `宽度和高度必须为非负值。`
- application log found at `C:\Users\sciman\AppData\Local\ClassroomToolkit\logs\error_20260422.log`
- repeated stack trace:
  - `System.Windows.Size..ctor(Double width, Double height)`
  - `System.Windows.Controls.ScrollViewer.OnLayoutUpdated(Object sender, EventArgs e)`
- recent change introduced `VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo` for image manager thumbnails.
- WPF `ScrollViewer` consumes `IScrollInfo.Extent*` and `Viewport*`; any transient negative width/height can surface as this exact `Size` constructor exception during layout update.

## Changes
1. Hardened `VirtualizingWrapPanel` dimensions:
   - `UpdateExtentAndViewport` now coerces viewport width/height to non-negative values.
   - thumbnail extent width/height are coerced before being exposed to `ScrollViewer`.
   - added `CoerceNonNegativeDimension` helper.
2. Extended regression coverage:
   - `ImageManagerTouchFlowContractTests` now asserts the non-negative coercion helper exists.

## Commands
1. `Get-Content "$env:LOCALAPPDATA\ClassroomToolkit\logs\error_20260422.log" -Tail 160`
2. `Get-Content "$env:LOCALAPPDATA\ClassroomToolkit\logs\app_20260422.log" -Tail 120`
3. `Get-Content "$env:LOCALAPPDATA\ClassroomToolkit\settings.json" -Raw`
4. `DOTNET_ROLL_FORWARD=LatestMajor dotnet build ClassroomToolkit.sln -c Debug`
5. `DOTNET_ROLL_FORWARD=LatestMajor dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ImageManagerTouchFlowContractTests"`
6. `DOTNET_ROLL_FORWARD=LatestMajor dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
7. `DOTNET_ROLL_FORWARD=LatestMajor dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`

## Key Output
- error log contained four repeated `Dispatcher.UnhandledException` entries between `01:51:48` and `01:53:11`, all with the same `ScrollViewer.OnLayoutUpdated -> Size..ctor` stack.
- build: passed
- targeted image manager contract tests: passed (3/3)
- full test: passed (3398/3398)
- contract/invariant: passed (28/28)
- hotspot review: passed for `VirtualizingWrapPanel.cs` and `ImageManagerTouchFlowContractTests.cs`.

## SDK Note
- repository `global.json` pins SDK `10.0.201` with `latestPatch`; current machine has `10.0.202`.
- gates were run with `DOTNET_ROLL_FORWARD=LatestMajor` to avoid changing `global.json`.

## Rollback
1. revert `src/ClassroomToolkit.App/Photos/VirtualizingWrapPanel.cs`
2. revert `tests/ClassroomToolkit.Tests/ImageManagerTouchFlowContractTests.cs`

## Residual Risk
- this fix addresses the negative dimension path reported by WPF layout.
- real-device relaunch is still required to confirm no further runtime layout exceptions occur during image manager thumbnail scrolling.
