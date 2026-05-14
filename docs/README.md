# ClassroomToolkit 文档目录

本目录保存架构、计划、验证、证据和运行手册。日常入口优先看根目录 `README.md` 与 `使用指南.md`；需要接手开发、审查风险或准备发布时，再进入下面的专项文档。

## 推荐阅读顺序

1. [README](../README.md)：项目定位、快速开始、本地数据、门禁和主要入口。
2. [使用指南](../使用指南.md)：教师课堂使用流程。
3. [tech-debt-backlog](./tech-debt-backlog.md)：当前低风险优化与稳定性任务清单。
4. [handover](./handover.md)：历史重构交接信息；其中日期较早的自动化统计只作为背景，当前状态以代码、测试和最新 evidence 为准。
5. [change-evidence](./change-evidence/)：每批变更的依据、命令、验证证据和回滚动作。

## 目录说明

- `architecture/`：架构边界、依赖矩阵和目标结构。
- `adr/`：已接受的架构决策记录。
- `change-evidence/`：变更证据与回滚说明。
- `compatibility/`：兼容性基线、矩阵和报告。
- `governance/`：治理报告、waiver、指标和 truth source。
- `plans/`：实施计划和阶段性任务。
- `runbooks/`：发布、迁移、现场验收和故障处理手册。
- `validation/`：验收模板、历史验证记录和专项证据。

## 维护约定

- 用户可见能力、安装方式、本地数据或门禁入口变化时，同步更新根目录 `README.md` 和必要的英文入口。
- 修改课堂数据格式、配置格式或持久化结构时，必须在 `change-evidence/` 记录兼容性、迁移和回滚。
- 历史计划与交接文档不作为最新事实裁决源；最新事实以代码、测试、当前 backlog 和最新证据为准。
