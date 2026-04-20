# 2026-04-20 工程化审查与稳健性加固（pass2）

## 1) 变更范围与目标归宿
- 边界：`Photos` 解析缓存、`Settings` JSON 存储保护、`Ink` WAL 并发锁粒度。
- 当前落点：`App/Infra` 内部实现与对应测试。
- 目标归宿：不改变业务语义/接口/数据格式前提下，修复高概率失效路径并提升并发可用性。

## 2) 问题分级与处置

### A. High - 新照片发现存在漏检窗口（正确性）
- 问题：`StudentPhotoResolver` 在缓存命中且目录时间戳未前进时，可能长时间跳过文件探测。
- 根因：旧策略只在短窗口内探测，后续直接返回 miss，依赖目录时间戳变化。
- 方案：引入“限频 miss 探测”策略（2s），在避免高频 I/O 的同时保证可恢复发现。
- 影响范围：`StudentPhotoResolver` 内部 miss 分支，不改变对外返回契约。
- 文件：
  - `src/ClassroomToolkit.App/Photos/StudentPhotoResolver.cs`
  - `src/ClassroomToolkit.App/Photos/StudentPhotoCachePolicy.cs`
  - `tests/ClassroomToolkit.Tests/StudentPhotoResolverTests.cs`

### B. High - JSON 设置写入被瞬时 I/O 失败误阻断（稳定性）
- 问题：上一次 `Load` 任意失败后会阻断覆盖写入，包含临时文件锁等可恢复场景。
- 根因：覆盖阻断条件过宽，把“操作性失败”和“内容损坏失败”混为一类。
- 方案：仅当 `Load` 失败类型为 `JsonException`（内容损坏）时阻断覆盖；瞬时 I/O 失败允许后续保存恢复。
- 影响范围：`JsonSettingsDocumentStoreAdapter` 内部状态机，不改变 JSON 格式与调用方式。
- 文件：
  - `src/ClassroomToolkit.Infra/Settings/JsonSettingsDocumentStoreAdapter.cs`
  - `tests/ClassroomToolkit.Tests/JsonSettingsDocumentStoreAdapterTests.cs`

### C. Medium - WAL 全局锁导致跨目录串行（性能/可维护性）
- 问题：`InkWriteAheadLogService` 使用全局单锁，跨目录 WAL 操作互相阻塞。
- 根因：锁粒度过粗。
- 方案：改为按 walPath 分段锁（`ConcurrentDictionary<string, object>`）。
- 影响范围：WAL 并发路径；读写语义不变。
- 文件：
  - `src/ClassroomToolkit.App/Ink/InkWriteAheadLogService.cs`
  - `tests/ClassroomToolkit.Tests/InkStorageDiagnosticsContractTests.cs`

## 3) 最小诊断矩阵（Codex 平台）
- `cmd`: `codex --version`  
  `exit_code`: 0  
  `key_output`: `codex-cli 0.121.0`
- `cmd`: `codex --help`  
  `exit_code`: 0  
  `key_output`: 正常输出 CLI 帮助
- `cmd`: `codex status`  
  `exit_code`: 1  
  `platform_na`: true  
  `reason`: `stdin is not a terminal`（非交互环境）  
  `alternative_verification`: 使用 `codex --version` + `codex --help` 补证平台可用性  
  `evidence_link`: 本文件第 3 节  
  `expires_at`: `2026-05-20`

## 4) 门禁执行记录（固定顺序）
1. build  
   `dotnet build ClassroomToolkit.sln -c Debug`  
   结果：PASS（0 warning, 0 error）
2. test  
   `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`  
   结果：PASS（3336 passed）
3. contract/invariant  
   `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`  
   结果：PASS（28 passed）
4. hotspot  
   `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`  
   结果：PASS

补充：变更相关子集测试  
`dotnet test ... --filter "FullyQualifiedName~StudentPhotoResolverTests|FullyQualifiedName~JsonSettingsDocumentStoreAdapterTests|FullyQualifiedName~InkWriteAheadLogServiceTests|FullyQualifiedName~InkStorageDiagnosticsContractTests"`  
结果：PASS（27 passed）

## 5) 热点人工复核（hotspot review）
- 复核对象：本次改动文件与其关键分支（缓存 miss、load/save 状态机、WAL 锁域）。
- 结论：
  - 未引入新对外契约变化。
  - 异常路径均保持非致命过滤策略。
  - 并发锁域缩小后，单 walPath 仍串行，跨路径可并发。

## 6) 回滚动作
- 代码回滚：`git restore --source=HEAD~1 -- <changed-files>`（按文件粒度）
- 测试回滚验证：重跑第 4 节门禁链确认恢复状态。
