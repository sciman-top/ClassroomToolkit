# ClassroomToolkit 文档目录

本目录保存架构、计划、验证、证据和运行手册。日常入口优先看根目录 `README.md` 与 `使用指南.md`；需要接手开发、审查风险、准备发布或判断“当前到底到哪了”时，再进入下面的专项文档。

## 当前真相入口（2026-06-13）

先看这些，再决定是否继续深挖历史资料：

1. [README](../README.md)：项目定位、快速开始、最新状态快照、门禁与入口。
2. [handover](./handover.md)：当前工作区的真实验证结果、最新交付切片、发布边界和下一步建议。
3. [tech-debt-backlog](./tech-debt-backlog.md)：当前工程问题、历史收口项和后续稳定性清理入口。
4. [change-evidence](./change-evidence/)：每批变更的依据、命令、验证证据和回滚动作；5 月底到 6 月初的入口优先看：
   - `20260531-whiteboard-entry-and-capture.md`
   - `20260531-toolbar-brush-slots-and-photo-undo.md`
   - `20260531-presentation-ink-retouch-wps-enter.md`
   - `20260602-paint-toolbar-brush-size-selection.md`
   - `20260602-photo-undo-runtime-history.md`
   - `20260602-window-layer-countdown.md`
   - `20260602-paint-settings-dialog-nre.md`

## 最新状态摘要

- 当前代码主线聚焦课堂高频场景：白板入口、快捷画笔粗细、图片 / PDF 批注撤销、PPT / WPS 叠加层 retouch、学生照片窗口层级。
- 最新本地门禁快照是：
  - `build` 通过
  - contract / invariant 过滤集通过
  - 完整测试集通过，3533/3533
- 因此“代码门禁全绿”与“课堂现场发布就绪”仍不是同一件事；发布判断请同时看 `handover` 和最新 `change-evidence`。

## 推荐阅读顺序

1. [README](../README.md)：项目定位、快速开始、本地数据、门禁和主要入口。
2. [使用指南](../使用指南.md)：教师课堂使用流程，包含白板入口、快捷画笔和图片 / PDF 讲解的最新用户向说明。
3. [handover](./handover.md)：开发接手、当前验证快照与发布边界。
4. [tech-debt-backlog](./tech-debt-backlog.md)：当前工程问题与历史收口项。
5. [change-evidence](./change-evidence/)：按日期查看每批改动的依据、验证和回滚。

## 目录说明

- `adr/`：已接受的架构决策记录。
- `architecture/`：架构边界、依赖矩阵和目标结构。
- `change-evidence/`：变更证据与回滚说明。
- `compatibility/`：兼容性基线、矩阵和报告。
- `governance/`：治理报告、waiver、指标和 truth source。
- `plans/`：实施计划和阶段性任务。
- `runbooks/`：发布、迁移、现场验收和故障处理手册。
- `validation/`：验收模板、历史验证记录和专项证据。

## 维护约定

- 用户可见能力、安装方式、本地数据、测试真相或门禁入口变化时，同步更新根目录 `README.md`、`README.en.md` 和必要的教师向文档。
- 当前验证状态发生变化时，优先更新 `docs/handover.md` 与 `docs/tech-debt-backlog.md`，不要只写一条新的 evidence 然后让入口文档继续漂移。
- 修改课堂数据格式、配置格式或持久化结构时，必须在 `change-evidence/` 记录兼容性、迁移和回滚。
- 历史计划与旧交接文档只作为背景；最新事实以代码、测试、最新 evidence 和当前入口文档为准。
