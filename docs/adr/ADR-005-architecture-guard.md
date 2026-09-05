# ADR-005: 架构守卫与 CI 阻断策略

- 日期: 2026-02-24
- 最后更新: 2026-08-17
- 状态: Accepted

## 决策

- 使用 `ArchitectureDependencyTests` 作为架构守卫。
- App 仅允许组合根接入 Infra，且只有 `Windowing` 目录可以直连 Interop；禁止新增违规。
- 守卫失败阻断合并。
- 允许目录不得为临时通过而扩张。

## 当前守卫重点

- App 层普通 UI / 场景文件不得直接依赖 `ClassroomToolkit.Interop`。
- Windowing Adapter / Executor 是唯一允许的 App -> Interop 目录 seam。
- 文档口径与守卫口径必须一致：architecture 与 project-status 中写明的边界，应与守卫保持同步。

## 说明

当前守卫按目录与项目引用验证依赖方向，不再维护过时的“6 文件白名单”。
