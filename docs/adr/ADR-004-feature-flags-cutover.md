# ADR-004: Feature Flag 切换与双跑策略

- 日期: 2026-02-24
- 最后更新: 2026-10-08
- 状态: Accepted（收窄适用范围）

## 背景

早期迁移阶段需要双跑与切换保护，但随着终态主线收口，若把 feature flag 扩展为普遍机制，会重新放大运行时复杂度。

## 决策

- Feature flag 仅用于非破坏性 cutover、兼容迁移和可选能力启用。
- 不把 feature flag 作为高风险主链长期并存机制。
- 高风险运行时主链应优先收敛为单一路径，而不是长期保留新旧双跑。

## 当前适用范围

- JSON / SQLite 迁移中的兼容切换。
- 必要时的短期兼容观察点。
- 当前学生名册 SQLite 快照恢复与 Ink 历史 SQLite adapter；二者仍有实际数据/回退消费者，不视为可清理的过期 flag。

## 不适用范围

- 场景互切、窗口层级、焦点管理、跨页刷新等高风险运行时主链。
- 不允许通过长期保留旧路径来掩盖主链设计未收口的问题。

## 当前 SQLite 例外边界（2026-10-08）

- `CTOOLKIT_USE_SQLITE_BUSINESS_STORE` 表示请求 SQLite 路由，`CTOOLKIT_ENABLE_EXPERIMENTAL_SQLITE_BACKEND` 是硬启用门；默认均关闭，且运行时依赖探测失败时回落到 Excel/sidecar。
- 两个开关由 `Startup/StorageBackendFeatureFlags` 在组合阶段读取。它们不是 `settings.ini` 或 JSON 用户设置，也不支持运行中切换。
- 学生名册的 Excel bridge 仍是成功读取时的权威来源，SQLite 保存可用快照恢复；Ink 历史保留 SQLite 与 sidecar bridge 的版本仲裁及双写结果。
- 只有在数据库文件处置、数据迁移、旧版兼容和故障回滚均有单独闭环后，才删除这些适配器或开关。
