# 04 Fixes And Regression

- Date: 2026-03-18
- Scope: Post-finding fix verification (Round-1)

## Implemented Fixes

1. `ImageManagerWindow` folder expansion lifecycle hardening
- File: `src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml.cs`
- Change:
  - `OnFolderExpanded` changed from `async void` body to fire-and-forget delegator.
  - New `OnFolderExpandedAsync` wraps full path with shutdown-safe exception handling.

2. Added regression contract tests
- File: `tests/ClassroomToolkit.Tests/ImageManagerWindowFolderExpandLifecycleContractTests.cs`
- Coverage:
  - verifies event handler delegates to async worker
  - verifies shutdown-safe catches are present

3. Added `ComObjectManager` lifecycle contract tests
- File: `tests/ClassroomToolkit.Tests/ComObjectManagerContractTests.cs`
- Coverage:
  - track guard for non-COM and disposed manager path
  - release path with non-fatal exception guard
  - dispose reverse-order release and state clearing contract

## Verification Commands

1. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ImageManagerWindowFolderExpandLifecycleContractTests"`
- Result: PASS (2/2)

2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~ImageManager"`
- Result: PASS (86/86)

3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ComObjectManagerContractTests"`
- Result: PASS (3/3)

## Residual Risks

- `WindowInteropRetryExecutor` still uses blocking sleep.
- `ComObjectManager` runtime COM integration tests are still missing (source-contract tests已补齐).
