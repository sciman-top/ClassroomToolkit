# 20260422 Touch First Compact Controls Phase 4 Implementation

- date: 2026-04-22
- scope: task7 deep thumbnail virtualization replacement
- risk_level: medium
- active_rule_path: D:\CODE\ClassroomToolkit\AGENTS.md
- current_boundary: image manager thumbnail grid virtualization without changing toolbar button count, free-drag rules, or existing touch-first command paths
- target_destination: replace `WrapPanel + CanContentScroll=False` thumbnail host with a true virtualizing panel while preserving existing visible-region thumbnail loading priority

## Basis
- repository fact: `ImageList` used `WrapPanel` and disabled content scrolling, so WPF realized the full thumbnail tree even though thumbnail loading already tried to prioritize visible rows.
- repository fact: no existing `VirtualizingWrapPanel` or equivalent reusable panel existed in the codebase.
- user constraints remain unchanged: keep controls compact, keep counts stable, keep free drag, favor touch-first operation.

## Changes
1. Added custom panel `VirtualizingWrapPanel`:
   - file: `src/ClassroomToolkit.App/Photos/VirtualizingWrapPanel.cs`
   - implements `VirtualizingPanel, IScrollInfo`
   - realizes only the visible thumbnail rows plus a small cache band
   - arranges thumbnails in a wrapped grid using measured item size
   - exposes pixel-based vertical scrolling to the hosting `ScrollViewer`
2. Swapped thumbnail `ImageList` items panel:
   - removed `WrapPanel IsItemsHost="True"`
   - added `<local:VirtualizingWrapPanel/>`
   - switched thumbnail `ImageList` to `ScrollViewer.CanContentScroll="True"`
   - enabled `VirtualizingPanel.IsVirtualizing="True"`
3. Preserved existing thumbnail scheduling layer:
   - no removal of `QueueVisibleRegionThumbnails`
   - visible-first thumbnail loading remains active on top of the new UI virtualization layer
4. Extended regression coverage:
   - `ImageManagerTouchFlowContractTests` now asserts the custom virtualizing panel is wired into the thumbnail grid

## Commands
1. `dotnet build ClassroomToolkit.sln -c Debug`
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ImageManagerTouchFlowContractTests"`
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
4. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`

## Key Output
- build: passed
- targeted virtualization contracts: passed (3/3)
- full test: passed (3398/3398)
- contract/invariant: passed (28/28)
- hotspot review: passed for `VirtualizingWrapPanel.cs` and `ImageManagerWindow.xaml`; no button-count increase introduced; free-drag behavior unchanged.

## Rollback
1. delete `src/ClassroomToolkit.App/Photos/VirtualizingWrapPanel.cs`
2. revert `src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml`
3. revert `tests/ClassroomToolkit.Tests/ImageManagerTouchFlowContractTests.cs`

## Residual Risk
- this panel assumes uniform tile sizing within the thumbnail grid, which matches the current item template and slider-driven width behavior.
- real-device validation is still needed for inertial scroll feel and very large directories, even though build/test gates are green.
