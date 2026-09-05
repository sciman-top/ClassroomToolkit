# ClassroomToolkit 文档目录

日常开发先看根目录 [README](../README.md) 与 [使用指南](../使用指南.md)。专项入口只保留当前仍有操作价值的文档。

## 当前入口

- [project-status](./project-status.md)：当前主线、最新状态快照与真值边界。
- [tech-debt-backlog](./tech-debt-backlog.md)：尚未关闭的工程问题。
- [current architecture](./architecture/README.md)：当前技术栈、依赖方向、模块 seam 与演进边界。
- [release checklist](./runbooks/release-checklist.md)：发布前检查。
- [change-evidence](./change-evidence/)：仅保留仍被现行规则或 waiver 消费的高风险证据。

## 长期文档

- `adr/`：已接受的架构决策。ADR-001/002（模块化单体分层、Application 端口）已并入 [architecture](./architecture/README.md) 作为现行真值，原文件从 Git 历史查询。
- `architecture/`：当前模块依赖与 Interop seam；日期型旧台账不作为真值入口。
- `compatibility/`：兼容性 SLA 与降级策略。
- `runbooks/`：发布、迁移、现场验收与恢复。

旧计划、阶段任务图、自动重构控制面、批次流水账和治理报告只保留在 Git 历史中，不再作为当前工作入口。

## 退役路径（勿复用）

以下路径已退役，不得再作为现行门禁或文档引用：`scripts/governance/*`、`.github/workflows/quality-gate.yml`、`.github/workflows/quality-gates.yml`、`azure-pipelines.yml`、`.gitlab-ci.yml`、`scripts/quality/check-governance-truth-source.ps1`、`scripts/validation/validate-stable-test-config.ps1`、`docs/governance/truth-source.md`、`docs/handover.md`。退役快照从 Git 历史查询。

## 维护约定

- 用户可见流程变化时更新 README 或使用指南。
- 发布边界变化时更新 project-status。
- 普通代码修改不要求同步多份状态文档，也不要求新增 evidence。
- 数据格式/迁移、Interop 生命周期、发布或不可逆修复才记录独立证据与回滚。
