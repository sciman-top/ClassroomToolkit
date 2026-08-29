# ClassroomToolkit 文档目录

日常开发先看根目录 [README](../README.md) 与 [使用指南](../使用指南.md)。专项入口只保留当前仍有操作价值的文档。

## 当前入口

- [handover](./handover.md)：当前开发边界、验证选择和剩余精简方向。
- [project-status](./project-status.md)：面向开发者的最新状态快照（2026-08-27）。
- [tech-debt-backlog](./tech-debt-backlog.md)：尚未关闭的工程问题。
- [current architecture](./architecture/README.md)：当前技术栈、依赖方向、模块 seam 与演进边界。
- [governance truth source](./governance/truth-source.md)：唯一门禁入口与 profile 分层。
- [release checklist](./runbooks/release-checklist.md)：发布前检查。
- [change-evidence](./change-evidence/)：仅保留仍被现行规则或 waiver 消费的高风险证据。

## 长期文档

- `adr/`：已接受的架构决策。
- `architecture/`：当前模块依赖与 Interop seam；日期型旧台账不作为真值入口。
- `compatibility/`：兼容性基线和现场矩阵。
- `runbooks/`：发布、迁移、现场验收与恢复。
- `validation/templates/`：现场验收模板。

旧计划、阶段任务图、自动重构控制面、批次流水账和治理报告只保留在 Git 历史中，不再作为当前工作入口。

## 维护约定

- 用户可见流程变化时更新 README 或使用指南。
- 发布边界变化时更新 handover。
- 普通代码修改不要求同步多份状态文档，也不要求新增 evidence。
- 数据格式/迁移、Interop 生命周期、发布或不可逆修复才记录独立证据与回滚。
