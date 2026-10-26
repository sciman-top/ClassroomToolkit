# SQLite 业务存储实验栈整体退役

- 日期: 2026-10-26
- 类别: 数据格式/迁移与回滚（ADR-004:33 约定的退役闭环）
- 决策: 依 ADR-004"不把 feature flag 作为高风险主链长期并存机制"与 tech-debt-backlog P2 挂账项，经用户授权执行整体裁剪（非转正）。

## 裁决依据

- 用户可达性为零：两个开关（`CTOOLKIT_USE_SQLITE_BUSINESS_STORE`、`CTOOLKIT_ENABLE_EXPERIMENTAL_SQLITE_BACKEND`）自仓库诞生起默认双关，仅出现在开发者内部文档（ADR/runbook/架构 README/源码），不出现在 README、使用指南、settings.ini、安装器或任何用户入口；真实课堂用户无法启用。
- SQLite 恒为镜像非权威：`students.xlsx` 与 sidecar JSON 始终是权威数据源，SQLite 仅是 bridge 失败时的恢复快照；默认状态下该兜底从未生效。
- 7 个月未转正：2026-03 随仓导入，9 月为加固高峰，此后无新增业务能力，无转正计划；"组合根重构收口"触发条件无成文判据，挂账将无限期延续。

## 退役四项闭环（ADR-004:33 / playbook 义务）

1. 现存文件盘点：仓库工作树、artifacts 与测试工程内无任何用户 `.sqlite3` 文件；仅 `tests/.tmp/` 存在测试运行产生的临时库（未跟踪、随测试创建/清理）。
2. 数据权威迁移：不适用——SQLite 从未成为权威来源，`students.xlsx`/sidecar 数据无需迁移。
3. 旧版兼容：历史遗留 `.studentworkbook.sqlite3` / `.inkhistory.sqlite3` 文件退役后成为惰性孤儿文件——无任何代码路径读取或写入它们，主路径（xlsx + sidecar）不受影响；文件保留在磁盘上无害，用户可自行删除。
4. 故障回滚：单提交可整体回退（见下）。

## 变更清单

- 删除生产 8 文件：`StudentWorkbookSqliteStoreAdapter`、`InkHistorySqliteStoreAdapter`、`RollStateVersionArbitrationPolicy`、`SqliteStorageUtilities`、`StudentWorkbookSqliteRollCallStoreAdapter`、`BusinessStorageBackend`、`RollCallWorkbookStoreResolver`、`StorageBackendFeatureFlags`。
- 删除接口 `IInkHistorySnapshotStore` 与其全部可空消费接线（PaintWindowFactory/PaintOverlayWindow 注入链、`AppCompositionRoot` 条件注册）；Ink 历史读写恒走 sidecar（`InkPersistenceService`）路径。
- 名册存储恒走 `RollCallWorkbookStoreAdapter`（Excel bridge），组合根直连。
- `StartupCompatibilityProbe` 移除 e_sqlite3.dll 存在性/加载探测及 RID 解析辅助；`prepare-distribution.ps1` 移除两处 `e_sqlite3.dll` 发行断言。
- 依赖移除：`Microsoft.Data.Sqlite 10.0.12`、`SQLitePCLRaw.bundle_e_sqlite3 3.0.5`（Infra.csproj）；发行包不再携带 `e_sqlite3.dll`。
- 删除测试 6 文件（Sqlite 适配器×2、SelectionPolicy、CapabilityPolicy、Resolver seam×2）及 3 个 native 探测辅助测试；两个样例字符串测试改为中性文案。

## 验证

- 全解决方案 Debug 构建 0 警告 0 错误。
- full 门禁（构建 + `Gate!=Performance` 全量测试单次执行 + hotspot 预算 + 依赖漏洞/升级审计 + analyzer latest-all 基线）于本批次收口执行，结果见当日 `artifacts/evidence/` 回执。

## 回滚入口

- `git revert <退役提交>` 单提交整栈恢复（生产/测试/csproj/脚本/文档同批）；依赖随 `.csproj` 一并还原。
- 用户机器上的惰性 `.sqlite3` 孤儿文件在回滚后可被恢复的 adapter 重新识别，无需数据处理。
