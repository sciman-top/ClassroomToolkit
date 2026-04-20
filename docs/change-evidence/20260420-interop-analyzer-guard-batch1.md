# 2026-04-20 Interop Analyzer Guard Batch 1

## Current Landing And Target
- boundary: `src/ClassroomToolkit.Interop/Presentation/*`
- current_landing: `KeyboardHook*`, `KeyboardHookSuppressionPolicy`, `Win32PresentationResolver.Native`, `WpsSlideshowNavigationHook.Interop`
- target_destination: clear a low-risk batch of existing Interop analyzer findings without changing runtime behavior, public data contracts, or external file/config formats

## Review Summary
- P1 fixed: `KeyboardHook` partial implementation lacked explicit `ClassroomToolkit.Interop.Presentation` namespace declarations in multiple files
- P1 fixed: `KeyboardHook.MapKey` used the legacy `Enum.IsDefined(typeof(...), value)` overload
- P1 fixed: `Win32PresentationResolver.GetProcessId` ignored the return value of `GetWindowThreadProcessId`
- P1 fixed: `GetModuleHandle` P/Invoke declarations did not use explicit Unicode string marshalling
- deferred: broader analyzer expansion is still out of scope; only the proven low-risk subset was addressed here

## Root Cause
- legacy Interop files predated current analyzer expectations and accumulated namespace / P/Invoke / API-usage inconsistencies
- these findings were surfaced during analyzer preflight, but they were small enough to repair locally without touching business logic

## TDD Evidence
- red command:
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~InteropAnalyzerContractTests"`
- red result:
  - 5 failures
  - representative evidence:
    - missing `namespace ClassroomToolkit.Interop.Presentation;`
    - legacy `Enum.IsDefined(typeof(VirtualKey), key)`
    - unchecked `GetWindowThreadProcessId`
    - missing `CharSet = CharSet.Unicode`
- green changes:
  - added source-contract tests for these four analyzer-sensitive patterns
  - moved all `KeyboardHook` partials to explicit `ClassroomToolkit.Interop.Presentation` namespace
  - switched to generic `Enum.IsDefined`
  - used `GetWindowThreadProcessId` return value when resolving PID
  - changed `GetModuleHandle` declarations to explicit Unicode marshalling
- green re-run:
  - same targeted command passed with `通过: 5, 失败: 0`

## Files Changed
- `src/ClassroomToolkit.Interop/Presentation/KeyboardHook.cs`
- `src/ClassroomToolkit.Interop/Presentation/KeyboardHook.Lifecycle.cs`
- `src/ClassroomToolkit.Interop/Presentation/KeyboardHook.Interop.cs`
- `src/ClassroomToolkit.Interop/Presentation/KeyboardHook.Callback.cs`
- `src/ClassroomToolkit.Interop/Presentation/KeyboardHookSuppressionPolicy.cs`
- `src/ClassroomToolkit.Interop/Presentation/Win32PresentationResolver.Native.cs`
- `src/ClassroomToolkit.Interop/Presentation/WpsSlideshowNavigationHook.Interop.cs`
- `tests/ClassroomToolkit.Tests/InteropAnalyzerContractTests.cs`

## Verification
- targeted_contract:
  - command: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~InteropAnalyzerContractTests"`
  - result: PASS
  - key_output: `通过: 5, 失败: 0`
- targeted_analyzer_preflight:
  - command: `dotnet build src/ClassroomToolkit.Interop/ClassroomToolkit.Interop.csproj -c Debug -p:EnableNETAnalyzers=true -p:AnalysisLevel=latest-recommended -p:WarningsAsErrors=CA1050%3BCA2263%3BCA1806%3BCA2101`
  - result: PASS
  - key_output: `0 个警告`, `0 个错误`
- build:
  - command: `dotnet build ClassroomToolkit.sln -c Debug`
  - result: PASS
  - key_output: `0 个警告`, `0 个错误`
- test:
  - command: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - result: PASS
  - key_output: `通过: 3360, 失败: 0`
- contract_invariant:
  - command: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - result: PASS
  - key_output: `通过: 28, 失败: 0`
- hotspot:
  - command: `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
  - result: PASS
  - key_output: `[hotspot] PASS - all .cs files within line budget (max=1200)`
- hotspot_manual_review:
  - file: `src/ClassroomToolkit.Interop/Presentation/KeyboardHook*.cs`
  - conclusion: namespace normalization and generic enum overload changes are structural only; hook callback logic is unchanged
  - file: `src/ClassroomToolkit.Interop/Presentation/Win32PresentationResolver.Native.cs`
  - conclusion: PID lookup now safely returns `0` when Win32 thread lookup fails; downstream `GetProcessName` already handles `0`
  - file: `src/ClassroomToolkit.Interop/Presentation/WpsSlideshowNavigationHook.Interop.cs`
  - conclusion: explicit Unicode marshalling only clarifies existing Windows behavior

## Rollback
- revert the changed Interop files and remove `tests/ClassroomToolkit.Tests/InteropAnalyzerContractTests.cs`
- rerun:
  - targeted `InteropAnalyzerContractTests`
  - targeted analyzer preflight
  - `build -> test -> contract/invariant -> hotspot`
