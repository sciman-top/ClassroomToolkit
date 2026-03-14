# ADR-001: 采用 Modular Monolith + Clean

- 日期: 2026-02-24
- 状态: Accepted

## 背景
当前系统已拆分项目，但 App/Services 仍存在编排与实现耦合，难以持续迁移。

## 决策
采用 Modular Monolith + Clean 分层：UI -> Application -> Domain，Infra/Interop 仅实现端口。

## 影响
- 支持渐进迁移与按 Phase 回滚。
- 增加 Application 层的契约和用例数量。
