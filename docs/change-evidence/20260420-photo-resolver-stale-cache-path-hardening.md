# 2026-04-20 Photo Resolver Stale Cache Path Hardening

## Scope
- issue_id: `photo-resolver-stale-cache-hit-after-delete`
- boundary: `src/ClassroomToolkit.App/Photos` + `tests/ClassroomToolkit.Tests`
- current_landing: `StudentPhotoResolver` cache-hit fast path
- target_destination: 缓存命中时不返回已删除文件路径，保持照片解析正确性与稳定性

## Risk
- level: `low`
- compatibility: 无外部接口变更；无数据格式/配置语义/持久化格式变更；向后兼容

## Change
1. `StudentPhotoResolver.ResolvePhotoPath`：
   - 缓存命中时增加 `File.Exists(cachedPath)` 校验；
   - 若缓存路径失效，移除目录缓存并走后续探测/索引逻辑。
2. 新增回归测试 `ResolvePhotoPath_ShouldNotReturnStalePath_WhenCachedFileWasDeleted`。

## Commands And Evidence
- `codex --version`
  - exit_code: `0`
  - key_output: `codex-cli 0.121.0`
- `codex --help`
  - exit_code: `0`
  - key_output: `Codex CLI` usage shown
- `codex status`
  - classification: `platform_na`
  - reason: `stdin is not a terminal`
  - alternative_verification: `codex --version` + `codex --help`
  - evidence_link: `this file`
  - expires_at: `2026-05-20`

- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~StudentPhotoResolverTests"`
  - exit_code: `0`
  - key_output: `通过 14 / 失败 0`

- `dotnet build ClassroomToolkit.sln -c Debug`
  - exit_code: `0`
  - key_output: `0 warnings, 0 errors`

- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - exit_code: `0`
  - key_output: `通过 3337 / 失败 0`

- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - exit_code: `0`
  - key_output: `通过 28 / 失败 0`

- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`
  - exit_code: `0`
  - key_output: `[hotspot] PASS - all .cs files within line budget (max=1200)`

## Rollback
1. Revert `src/ClassroomToolkit.App/Photos/StudentPhotoResolver.cs` in this change.
2. Revert `tests/ClassroomToolkit.Tests/StudentPhotoResolverTests.cs` added regression case.
3. Re-run gate sequence:
   - `dotnet build ClassroomToolkit.sln -c Debug`
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
   - contract filter command
   - hotspot command

