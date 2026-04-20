# 2026-04-20 Atomic Replace Consistency Hardening

## Scope
- Repo: `ClassroomToolkit`
- Goal: 收敛存储链路重复的 `TryReplaceOrOverwrite` 实现，统一原子替换回退行为，降低后续行为分叉风险。

## Rule Mapping
- Global: `R2 R5 R6 R8`
- Project: `C.2 fixed order gates`, `C.3 fail policy`, `C.4 evidence/rollback`

## Changes
- Added shared utility:
  - `src/ClassroomToolkit.Domain/Utilities/AtomicFileReplaceUtility.cs`
  - API: `AtomicFileReplaceUtility.ReplaceOrOverwrite(string tempPath, string targetPath)`
- Replaced duplicated local implementations with shared utility:
  - `src/ClassroomToolkit.Infra/Settings/JsonSettingsDocumentStoreAdapter.cs`
  - `src/ClassroomToolkit.Infra/Settings/IniSettingsStore.cs`
  - `src/ClassroomToolkit.Infra/Storage/StudentWorkbookStore.cs`
  - `src/ClassroomToolkit.App/Ink/InkPersistenceService.cs`
  - `src/ClassroomToolkit.App/Ink/InkStorageService.cs`
  - `src/ClassroomToolkit.App/Ink/InkWriteAheadLogService.cs`
- Added regression test:
  - `tests/ClassroomToolkit.Tests/AtomicFileReplaceUtilityTests.cs`
  - Covers replace path: target content is replaced and temp file no longer exists.
- Stabilized an existing flaky concurrency test:
  - `tests/ClassroomToolkit.Tests/StudentPhotoResolverTests.cs`
  - Change: `started.Wait(TimeSpan.FromSeconds(1))` -> `started.Wait(TimeSpan.FromSeconds(5))`
  - Reason: 降低高负载/调度抖动导致的偶发假失败。
- Added dispose-state regression assertion:
  - `tests/ClassroomToolkit.Tests/StudentPhotoResolverTests.cs`
  - New test: `Dispose_ShouldClearIndexLocks_AfterWarmupCache`
  - Purpose: 覆盖 Dispose 后可增长内部状态（`_indexLocks`）清空语义。
- Added disposed-warmup regression assertion:
  - `tests/ClassroomToolkit.Tests/StudentPhotoResolverTests.cs`
  - New test: `WarmupCache_AfterDispose_ShouldNotCreateNewCancellationTokenSource`
  - Purpose: 覆盖释放后 `WarmupCache` 不会重新分配取消令牌。

## Gate Evidence
1. `dotnet build ClassroomToolkit.sln -c Debug -m:1`
   - exit_code: `0`
   - key_output: `0 errors` (warnings only: transient file lock retries)
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1`
   - exit_code: `0`
   - key_output: `Passed 3332`
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - exit_code: `0`
   - key_output: `Passed 28`
4. `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`
   - exit_code: `0`
   - key_output: `[hotspot] PASS - all .cs files within line budget (max=1200)`
5. `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile quick -Configuration Debug`
   - exit_code: `0`
   - key_output: `[quality] ALL PASS`

## Risk and Rollback
- Risk level: `low`
- Rollback:
  1. `git restore --source=HEAD~1 src/ClassroomToolkit.Domain/Utilities/AtomicFileReplaceUtility.cs`
  2. `git restore --source=HEAD~1 src/ClassroomToolkit.Infra/Settings/JsonSettingsDocumentStoreAdapter.cs`
  3. `git restore --source=HEAD~1 src/ClassroomToolkit.Infra/Settings/IniSettingsStore.cs`
  4. `git restore --source=HEAD~1 src/ClassroomToolkit.Infra/Storage/StudentWorkbookStore.cs`
  5. `git restore --source=HEAD~1 src/ClassroomToolkit.App/Ink/InkPersistenceService.cs`
  6. `git restore --source=HEAD~1 src/ClassroomToolkit.App/Ink/InkStorageService.cs`
  7. `git restore --source=HEAD~1 src/ClassroomToolkit.App/Ink/InkWriteAheadLogService.cs`
  8. `git restore --source=HEAD~1 tests/ClassroomToolkit.Tests/AtomicFileReplaceUtilityTests.cs`
  9. `git restore --source=HEAD~1 tests/ClassroomToolkit.Tests/StudentPhotoResolverTests.cs`
