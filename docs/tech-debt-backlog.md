# ClassroomToolkit 技术债与稳定性优化清单

## 概览

本清单聚焦 `正确性优先、稳定性优先、兼容性优先、可维护性优先`。
范围限定为低风险、局部、可验证、可回滚的改进；默认不触碰外部接口、配置格式、数据格式与线程模型。

## 排序原则

- `P0`：存在真实风险，已复现、已静态证明或直接影响关闭/恢复/持久化一致性
- `P1`：提升可观测性、异常隔离、资源释放安全，收益高且兼容性风险低
- `P2`：清理重复代码、命名与结构优化、局部性能优化
- `Deferred`：高风险、低证据、难验证，默认暂缓

## P0

### Task 1: 统一照片缓存与预热关闭路径

**Description:** 继续审查 `StudentPhotoResolver` 及其调用链，确保窗口关闭、ViewModel 释放、后台预热之间不存在释放后回填状态、重复取消或失效缓存继续复用的问题。

**Acceptance criteria:**
- [ ] `Dispose` 后不再新增缓存项、不再继续预热、不再持有可增长状态
- [ ] 关闭窗口与切换班级时，照片路径解析保持兼容
- [ ] 新增至少 1 个并发/关闭路径回归测试

**Verification:**
- [ ] 定向测试：`dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~StudentPhotoResolverTests|FullyQualifiedName~RollCallViewModelPhotoPathRefreshTests"`
- [ ] 全量测试：`dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1`

**Dependencies:** None

**Files likely touched:**
- `src/ClassroomToolkit.App/Photos/StudentPhotoResolver.cs`
- `src/ClassroomToolkit.App/ViewModels/RollCallViewModel*.cs`
- `tests/ClassroomToolkit.Tests/StudentPhotoResolverTests.cs`

**Estimated scope:** S

### Task 2: 审查点名预加载并发状态机 `[Done 2026-04-18]`

**Description:** 复核 `RollCallViewModel` 预加载逻辑，重点检查 `_preloadTask/_preloadedResult/_preloadedPath` 的竞争更新、旧任务覆盖新任务、异常路径未清理与关闭取消路径。

**Acceptance criteria:**
- [ ] 旧预加载任务不能覆盖新任务状态
- [ ] 关闭或取消时不触发 UI 线程越界更新
- [ ] 预加载失败时回退路径可诊断

**Verification:**
- [ ] 定向测试：`dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~RollCallViewModelPreloadConcurrencyTests"`
- [ ] 热点人工复核 `WarmupData/LoadDataAsync/TryConsumePreloadedResult`

**Dependencies:** Task 1

**Files likely touched:**
- `src/ClassroomToolkit.App/ViewModels/RollCallViewModel.Data.cs`
- `tests/ClassroomToolkit.Tests/RollCallViewModelPreloadConcurrencyTests.cs`

**Estimated scope:** S

### Task 3: 存储层原子写回退策略一致性复核

**Description:** 统一检查 `settings / ink / workbook / wal` 的临时文件写入与 `File.Replace` 回退策略，重点确认锁文件、权限异常、部分失败后的残留临时文件与覆盖语义是否一致。

**Acceptance criteria:**
- [ ] 同类存储组件的原子写/回退策略一致
- [ ] 出现回退时不破坏现有文件格式与可读性
- [ ] 临时文件清理策略明确且有测试覆盖

**Verification:**
- [ ] 定向测试：`dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~JsonSettingsDocumentStoreAdapterTests|FullyQualifiedName~InkPersistenceServiceTests|FullyQualifiedName~InkStorageServiceTests|FullyQualifiedName~StudentWorkbookStoreTests"`
- [ ] 手工复核 `TryReplaceOrOverwrite` 调用点

**Dependencies:** None

**Files likely touched:**
- `src/ClassroomToolkit.Infra/Settings/JsonSettingsDocumentStoreAdapter.cs`
- `src/ClassroomToolkit.App/Ink/InkPersistenceService.cs`
- `src/ClassroomToolkit.App/Ink/InkStorageService.cs`
- `src/ClassroomToolkit.Infra/Storage/StudentWorkbookStore.cs`

**Estimated scope:** M

## P1

### Task 4: 日志与诊断链补齐最小可观测性 `[Done 2026-04-18]`

**Description:** 继续补齐 `FileLoggerProvider`、关键后台任务和异常吞掉路径的诊断输出，避免静默失败但不改变现有容错策略。

**Acceptance criteria:**
- [ ] 关键后台失败至少输出 `source + exception type + message`
- [ ] 不新增用户可见弹窗
- [ ] 不引入额外依赖

**Verification:**
- [ ] 定向测试：`dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~FileLoggerProviderTests|FullyQualifiedName~FileLoggerProviderShutdownSafetyContractTests|FullyQualifiedName~SafeTaskRunnerTests"`
- [ ] 热点人工复核日志关闭与后台任务取消路径

**Dependencies:** None

**Files likely touched:**
- `src/ClassroomToolkit.Infra/Logging/FileLoggerProvider.cs`
- `src/ClassroomToolkit.App/Utilities/SafeTaskRunner.cs`
- `tests/ClassroomToolkit.Tests/FileLoggerProviderTests.cs`

**Estimated scope:** S

### Task 5: Hook/Interop 生命周期边界复核 `[Done 2026-04-18]`

**Description:** 审查 `GlobalHookService`、`KeyboardHook`、`WpsSlideshowNavigationHook` 的启动/停止/Dispose 路径，重点看重复释放、停止失败后状态残留、回调解绑一致性。

**Acceptance criteria:**
- [ ] `Dispose`、`Stop`、注册失败回滚路径具备幂等性
- [ ] 事件解绑与对象释放顺序稳定
- [ ] 失败路径保留最小诊断

**Verification:**
- [ ] 定向测试：`dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~GlobalHookServiceTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests"`
- [ ] 热点人工复核低层 hook stop/dispose 路径

**Dependencies:** None

**Files likely touched:**
- `src/ClassroomToolkit.Services/Input/GlobalHookService.cs`
- `src/ClassroomToolkit.Interop/Presentation/KeyboardHook*.cs`
- `src/ClassroomToolkit.Interop/Presentation/WpsSlideshowNavigationHook*.cs`

**Estimated scope:** M

### Task 6: UI 关闭路径定时器与后台任务收敛

**Description:** 审查主窗口、图片管理器、画板窗口的 `DispatcherTimer`、取消令牌、后台任务收尾路径，避免关闭后继续触发 Tick 或释放后访问 UI。

**Acceptance criteria:**
- [x] 关闭后不再触发无主定时器 Tick
- [x] 后台任务取消顺序稳定，不出现重复释放异常
- [x] 至少补 1 个关闭路径回归测试或契约测试

**Verification:**
- [x] 定向测试：`dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~MainWindow|FullyQualifiedName~ImageManagerWindow|FullyQualifiedName~PhotoOverlay|FullyQualifiedName~PaintWindowOrchestratorLifecycleContractTests"`
- [x] 热点人工复核 `OnClosed/CompleteClose/Dispose` 路径

**Dependencies:** None

**Files likely touched:**
- `src/ClassroomToolkit.App/MainWindow*.cs`
- `src/ClassroomToolkit.App/Photos/ImageManagerWindow*.cs`
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow*.cs`

**Estimated scope:** M

## P2

### Task 7: 重复原子写辅助逻辑收敛

**Description:** 在不改变行为的前提下，评估是否将重复的 `temp + replace/copy + cleanup` 模式收敛到共享 helper；只有在证据充分且调用点一致时才实施。

**Acceptance criteria:**
- [ ] 仅在调用点语义一致时收敛
- [ ] 不改变异常语义与文件编码
- [ ] 变更后测试与合同检查全绿

**Verification:**
- [ ] 定向测试覆盖相关存储组件
- [ ] `git diff` 热点人工复核，确认无隐式行为变化

**Dependencies:** Task 3

**Files likely touched:**
- `src/ClassroomToolkit.App/Ink/*.cs`
- `src/ClassroomToolkit.Infra/Settings/*.cs`
- `src/ClassroomToolkit.Infra/Storage/*.cs`

**Estimated scope:** M

### Task 8: 局部死代码与重复诊断字符串清理

**Description:** 清理确认无用的局部辅助方法、重复诊断拼接和低价值包装层，但不做跨模块重构。

**Acceptance criteria:**
- [ ] 删除项有引用证据或契约测试保护
- [ ] 不改公共接口
- [ ] 代码行数减少且可读性提升

**Verification:**
- [ ] `rg` 复查无残留引用
- [ ] 全量测试通过

**Dependencies:** None

**Files likely touched:**
- 待定，按具体目标收敛

**Estimated scope:** S

## Deferred

### Task D1: 日志基础设施替换

- 状态: 暂缓
- 原因: 现有证据不足以支持改为有界通道、异步文件流复用或结构化日志方案
- 风险: 可能改变吞吐、时序与关闭行为

### Task D2: 大范围线程模型调整

- 状态: 暂缓
- 原因: 涉及 WPF Dispatcher、Interop Hook、后台任务取消协作，验证成本高
- 风险: 高

### Task D3: 存储格式迁移或依赖升级

- 状态: 暂缓
- 原因: 触及兼容性红线，需要单独方案、迁移与回滚设计
- 风险: 高

## 检查点

### Checkpoint A: 完成 P0 后

- [ ] `build -> test -> contract/invariant -> hotspot` 全部通过
- [ ] 关闭/恢复/预热/持久化路径没有新增兼容性回归
- [ ] 所有改动都已写入 `docs/change-evidence/`

### Checkpoint B: 完成 P1 后

- [ ] 后台异常可定位
- [ ] 关键生命周期路径具备幂等性与最小诊断
- [ ] 没有新增用户可见干扰

### Checkpoint C: 准备进入 P2 前

- [ ] 只保留“收益明确、风险低、易回滚”的清理项
- [ ] 任何跨模块抽象先单独评审，默认不做

## 本轮建议执行顺序

1. Task 2: 点名预加载并发状态机
2. Task 3: 存储层原子写回退策略一致性复核
3. Task 5: Hook/Interop 生命周期边界复核
4. Task 6: UI 关闭路径定时器与后台任务收敛
5. Task 4: 日志与诊断链补齐最小可观测性
