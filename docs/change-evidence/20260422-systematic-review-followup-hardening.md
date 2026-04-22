# 2026-04-22 系统化审查后续修复（阻塞等待守卫 + 原子写一致性）

## 基本信息
- `issue_id`: `review-20260422-followup-hardening`
- `attempt_count`: `1`
- `clarification_mode`: `direct_fix`
- `clarification_scenario`: `N/A`
- `rule_ids`: `R1,R2,R3,R6,R8`
- `risk_level`: `Low-Medium`
- `boundary`: `Domain/Infra/App` 的文件原子写路径与合同测试
- `current_destination`:
  - 阻塞等待使用缺少仓库级防新增守卫
  - 多处 `temp + replace/move + cleanup` 写入逻辑重复实现
- `target_destination`:
  - 新增阻塞等待 allow-list 合同测试，阻止新增高风险阻塞模式
  - 用统一 helper 收敛原子写入流程，保持行为不变并统一清理策略

## 变更摘要
1. 新增阻塞等待合同测试：
   - `tests/ClassroomToolkit.Tests/BlockingWaitUsageContractTests.cs`
   - 扫描 `src/**/*.cs` 中 `.GetAwaiter().GetResult()` 与 `.Wait(`。
   - 通过 allow-list 固化现有历史使用点，禁止新增。
2. 扩展原子写工具：
   - `AtomicFileReplaceUtility` 新增 `WriteAtomically`，统一目录创建、替换/移动、临时文件清理。
3. 收敛重复原子写逻辑（不改外部语义）：
   - `JsonSettingsDocumentStoreAdapter`
   - `IniSettingsStore`
   - `InkPersistenceService`
   - `InkStorageService`
   - `InkWriteAheadLogService`
   - `InkExportManifestUtilities`
4. 补充工具测试：
   - `AtomicFileReplaceUtilityTests` 新增覆盖正常替换与失败时临时文件清理。
5. 延续上一切片：
   - `StudentPhotoResolver` 学生粒度缓存失效 + 对应回归测试保持生效。
6. 阻塞等待专项增量收敛：
   - `RollCallViewModel.Data.cs` 移除 `GetAwaiter().GetResult()`，改为在已完成任务路径下读取 `Task.Result`。
   - faulted 路径改为 `ExceptionDispatchInfo.Capture(failure).Throw()` 保持原异常语义。
   - `BlockingWaitUsageContractTests` allow-list 同步收紧，移除已清理条目。
7. 阻塞等待专项收敛完成：
   - `KeyboardHook.Start()` 改为同步重试路径，不再通过 async `.GetResult()` 阻塞。
   - `FileLoggerProvider.WaitTaskSafely` 改为轮询超时等待，不再调用 `Task.Wait(timeoutMs)`。
   - `BlockingWaitUsageContractTests` allow-list 清空，`src` 不再存在阻塞等待模式。

## 执行命令与证据
- `dotnet test ... --filter "BlockingWaitUsageContractTests|AtomicFileReplaceUtilityTests|IniSettingsStoreSaveTests|JsonSettingsDocumentStoreAdapterTests|InkStorageDiagnosticsContractTests|InkStorageServiceTests|InkPersistenceServiceTests|InkWriteAheadLogServiceTests|StudentPhotoResolverTests"` -> `exit_code=0`
- `dotnet test ... --filter "RollCallViewModelPreloadConcurrencyTests|BlockingWaitUsageContractTests"` -> `exit_code=0`
- `dotnet test ... --filter "BlockingWaitUsageContractTests|InteropHookLifecycleContractTests|FileLoggerProviderTests|FileLoggerProviderShutdownSafetyContractTests"` -> `exit_code=0`
- `dotnet build ClassroomToolkit.sln -c Debug` -> `exit_code=0`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` -> `exit_code=0`，`3421 passed`
- `dotnet test ... --filter "ArchitectureDependencyTests|InteropHookLifecycleContractTests|InteropHookEventDispatchContractTests|GlobalHookServiceLifecycleContractTests|CrossPageDisplayLifecycleContractTests"` -> `exit_code=0`，`28 passed`
- `powershell -File scripts/quality/check-hotspot-line-budgets.ps1` -> `exit_code=0`
- `powershell -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug` -> `exit_code=0`，全链路通过

## N/A 记录
### `platform_na`
- 本次无

### `gate_na`
- 本次无

## hotspot 人工复核结论
- 复核文件：
  - `src/ClassroomToolkit.Domain/Utilities/AtomicFileReplaceUtility.cs`
  - `src/ClassroomToolkit.Infra/Settings/IniSettingsStore.cs`
  - `src/ClassroomToolkit.Infra/Settings/JsonSettingsDocumentStoreAdapter.cs`
  - `src/ClassroomToolkit.App/Ink/Ink*.cs`
  - `tests/ClassroomToolkit.Tests/BlockingWaitUsageContractTests.cs`
- 关注点：
  - helper 收敛后异常传播语义是否变化
  - 临时文件清理失败是否仍保留最小诊断
  - 合同测试是否误报现有允许路径
- 结论：门禁与定向测试全绿，未见兼容性回归。

## 风险与待办
- `InvalidateStudentCache` 业务调用点接入：当前仓内未发现直接“学生照片写入/替换”的稳定入口，暂未做盲目注入，避免误触非学生照片链路。
- 历史阻塞等待点仍保留在 allow-list（KeyboardHook/RollCallViewModel/FileLoggerProvider），需后续专项改造而非本次批量改线程语义。
- 阻塞等待点已全部从 `src` 清理，后续需关注的是行为层面回归（尤其 hook 启动时序与日志后台关闭时序）。

## 回滚方案
1. 回滚目标文件：
   - `src/ClassroomToolkit.Domain/Utilities/AtomicFileReplaceUtility.cs`
   - `src/ClassroomToolkit.Infra/Settings/IniSettingsStore.cs`
   - `src/ClassroomToolkit.Infra/Settings/JsonSettingsDocumentStoreAdapter.cs`
   - `src/ClassroomToolkit.App/Ink/InkExportManifestUtilities.cs`
   - `src/ClassroomToolkit.App/Ink/InkPersistenceService.cs`
   - `src/ClassroomToolkit.App/Ink/InkStorageService.cs`
   - `src/ClassroomToolkit.App/Ink/InkWriteAheadLogService.cs`
   - `tests/ClassroomToolkit.Tests/AtomicFileReplaceUtilityTests.cs`
   - `tests/ClassroomToolkit.Tests/BlockingWaitUsageContractTests.cs`
2. 回滚动作：
   - 恢复各文件原有 `temp + replace/move + cleanup` 本地实现
   - 删除新增合同测试与新增工具测试
3. 回滚后复测：
   - `dotnet build ClassroomToolkit.sln -c Debug`
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
