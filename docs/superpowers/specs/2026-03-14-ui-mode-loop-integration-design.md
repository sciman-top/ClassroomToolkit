# sciman课堂工具箱 UI Mode Loop 接入设计

日期：2026-03-14  
状态：Draft for Review  
范围：为 ClassroomToolkit 的 UI/窗口体系终态重构接入自动执行 loop  
关联设计：`docs/superpowers/specs/2026-03-14-ui-window-system-design.md`

## 1. 背景

当前仓库已经存在两类自动执行能力：

- 通用内核：`autonomous-execution-loop`
- 仓库适配层：`autonomous-refactor-loop`

其中，repo-local 的执行图与状态文件目前服务于“目标架构迁移/收尾”路径：

- `docs/refactor/tasks.json`
- `.codex/refactor-state.json`

这套执行层不适合直接承载本次“UI/窗口体系终态重构”，原因如下：

- 目标域不同：当前任务图面向 architecture gap closure，而非 UI overhaul
- governing docs 不同：本次工作以 UI/window system spec 为准，不是原有 target-architecture 文档
- 验证方式不同：UI 重构既有自动验证，也有必须停下的人工审美 gate
- 风险类型不同：窗口行为基线、owner/topmost/focus/DPI 等回归风险高于普通代码整理

因此，本设计的目标是：

- 不打乱现有架构迁移 loop
- 为 UI 终态重构新增一套独立、可自动推进的 mode
- 明确 `autonomous-execution-loop` 与 `autonomous-refactor-loop` 的职责边界
- 为后续 skill 改造提供直接的实现依据

## 2. 设计目标

### 2.1 主要目标

- 为本仓库新增 repo-local mode：`ui-window-system`
- 让 generic 抽象 mode：`ui-overhaul` 可以稳定映射到该 repo-local mode
- 将 UI 重构的 tasks/state/config 与现有架构迁移执行层彻底隔离
- 支持自动执行，但在视觉冻结点必须停下等待人工确认
- 保持现有 wrapper、lock、machine-readable status 语义兼容

### 2.2 非目标

- 不重写 generic loop 的整体状态机协议
- 不废弃现有 `architecture-refactor` 路径
- 不把 UI 重构任务混入当前 `docs/refactor/tasks.json`
- 不把 ClassroomToolkit 私有规则硬编码进 generic loop

## 3. 分层职责

### 3.1 `autonomous-execution-loop`

角色：通用执行内核

职责：

- 定义执行协议
- 定义通用 config/profile/state schema
- 定义 wrapper、lock、ownership、machine-readable status
- 定义抽象 mode 家族
- 执行 mode-aware compatibility bridge

不负责：

- ClassroomToolkit 的窗口清单
- ClassroomToolkit 的 spec 路径
- ClassroomToolkit 的验证命令
- ClassroomToolkit 的人工视觉 gate

### 3.2 `autonomous-refactor-loop`

角色：ClassroomToolkit repo-local adapter

职责：

- 注册本仓库支持的具体 mode
- 维护 mode 到 tasks/state/doc/verify/gate 的映射
- 提供仓库私有的验证链与停止条件
- 接管仓库内路径、人工 gate 和回归矩阵

不负责：

- 重写 generic 状态机协议
- 发明另一套不兼容的 wrapper/status/lock 规范

### 3.3 Mode package

角色：任务域实现

本次新增：

- generic mode family：`ui-overhaul`
- repo-local mode：`ui-window-system`

关系：

- `ui-overhaul` 是跨仓库的抽象家族
- `ui-window-system` 是 ClassroomToolkit 的具体实现

## 4. Mode 命名与映射

### 4.1 抽象 mode

- `architecture-refactor`
- `ui-overhaul`
- 未来可扩展：`verification-freeze`、`review-fix`、`optimization`

### 4.2 repo-local 具体 mode

建议首批支持：

- `architecture-refactor`
- `ui-window-system`

### 4.3 映射规则

| generic family | repo-local mode | 说明 |
|---|---|---|
| `architecture-refactor` | `architecture-refactor` | 现有目标架构迁移/收尾 |
| `ui-overhaul` | `ui-window-system` | 本次 UI/窗口体系终态重构 |

规则：

- generic 层先识别 family
- repo-local adapter 决定本仓库是否实现该 family
- 如果实现，则返回具体 mode
- 如果未实现，则当前仓库实现统一返回 `BLOCKED_NEEDS_HUMAN`
- generic family/bootstrap 扩展属于后续通用层第二阶段，不属于本次 repo-local 接入范围

`-Mode` 输入归一化规则：

- 若传入 concrete mode，例如 `ui-window-system`，repo-local adapter 直接按该 mode 解析
- 若传入 family，例如 `ui-overhaul`，repo-local adapter 负责解析到唯一 concrete mode
- 若一个 family 对应多个 concrete mode 且未显式指定，则返回 `BLOCKED_NEEDS_HUMAN`
- 若传入的 mode 与 family 冲突，则返回 `BLOCKED_NEEDS_HUMAN`

## 5. 文件布局

UI mode 不复用现有架构迁移执行图，建议单独建档：

### 5.1 新增文件

- `docs/ui-refactor/tasks.json`
- `.codex/ui-window-system-state.json`
- `.codex/ui-window-system.config.json`
- `docs/superpowers/plans/2026-03-14-ui-mode-loop-integration-implementation-plan.md`
- `docs/validation/ui-window-system-progress.md`
- `docs/validation/ui-window-system-acceptance.md`

### 5.2 保持原状

以下文件继续服务于现有架构迁移 mode，不与 UI mode 混用：

- `docs/refactor/tasks.json`
- `.codex/refactor-state.json`

### 5.3 governing docs 与 reconciliation

UI mode 的 authoritative governing docs 建议固定为：

- `docs/superpowers/specs/2026-03-14-ui-window-system-design.md`
- `docs/superpowers/specs/2026-03-14-ui-mode-loop-integration-design.md`
- `docs/superpowers/plans/2026-03-14-ui-mode-loop-integration-implementation-plan.md`
- `docs/validation/ui-window-system-progress.md`
- `docs/validation/ui-window-system-acceptance.md`

UI mode 必须自带 governing reconciliation 配置，至少包含：

- `doc_paths`
- `last_reconciled_at`
- `doc_sync_policy`
- `completion_gate_policy`

建议：

- `doc_sync_policy = spec > integration-design > implementation-plan > progress > acceptance`
- `completion_gate_policy = wrapper must refuse ALL_AUTOMATABLE_TASKS_DONE when any authoritative governing doc is newer than reconciliation stamp`

## 6. Mode Registry

repo-local adapter 应引入 mode registry。建议使用一个轻量 JSON 配置，概念上类似：

```json
{
  "modes": [
    {
      "mode_id": "architecture-refactor",
      "mode_family": "architecture-refactor",
      "tasks_file": "docs/refactor/tasks.json",
      "state_file": ".codex/refactor-state.json"
    },
    {
      "mode_id": "ui-window-system",
      "mode_family": "ui-overhaul",
      "tasks_file": "docs/ui-refactor/tasks.json",
      "state_file": ".codex/ui-window-system-state.json"
    }
  ]
}
```

每个 mode 至少需要声明：

- `mode_id`
- `mode_family`
- `tasks_file`
- `state_file`
- `governing_docs`
- `verification.commands`
- `manual_gates`
- `stop_rules`

建议再增加：

- `governing_reconciliation.doc_paths`
- `governing_reconciliation.doc_sync_policy`
- `lock_scope`
- `helper_scripts`

## 7. 主从关系

建议固定为：

- `autonomous-execution-loop`：主协议
- `autonomous-refactor-loop`：repo adapter
- `ui-window-system`：repo-local mode payload

具体含义：

- generic loop 负责决定“循环怎么跑”
- repo-local adapter 负责决定“这个仓库跑哪套任务图”
- mode payload 负责定义“这次 UI 重构到底做什么”

补充约束：

- top-level `mode` 字段一律表示 repo-local 具体 mode，例如 `ui-window-system`
- family 通过 `mode_family` 表达，例如 `ui-overhaul`
- 不再允许 state 写执行器名、tasks 写 mode 名的混用方式

## 8. Compatibility Bridge 行为

### 8.1 现状问题

当前 generic loop 发现 repo-local adaptation layer 后，会优先复用它。这在单 mode 仓库里是合理的，但在多 mode 仓库里不够精确。

### 8.2 新规则

bridge 逻辑应改为按 mode 路由，而不是按“是否存在 repo-local loop”路由。

执行顺序：

1. generic loop 接收请求 mode/family
2. generic loop 检查仓库是否存在 repo-local adapter
3. 如果存在，查询该 adapter 是否支持当前 mode family
4. 若支持：
   - 由 repo-local adapter 返回具体 mode
   - generic loop 按 repo-local mapping 执行
5. 若不支持：
   - generic loop 不得误接管到别的 repo-local mode
   - 统一返回 `BLOCKED_NEEDS_HUMAN`

当前阶段不采用“unsupported family 时 generic bootstrap”路径，原因是本仓库已存在 repo-local adapter；此时 mode 不支持属于仓库配置缺口，不属于本次 repo-local 接入的 generic bootstrap 场景

### 8.3 关键约束

- `architecture-refactor` adapter 不能误接管 `ui-overhaul`
- `ui-window-system` mode 只能使用 UI mode 的 governing docs 和 tasks/state
- generic bridge 必须 mode-aware，而不是 presence-aware

## 9. UI Tasks 设计

### 9.1 阶段划分

UI mode 的任务图建议分为：

- `foundation`
- `controls`
- `window-shell`
- `main-scenes`
- `management-and-settings`
- `dialog-tail`
- `visual-regression`

说明：

- 上述阶段划分属于 `ui-window-system` mode payload
- 这份 loop integration 文档只要求 mode payload 存在可执行阶段，不要求在此文档里展开所有视觉任务细节

### 9.2 单任务字段

每个任务建议至少包含：

- `id`
- `title`
- `stage`
- `priority`
- `order`
- `depends_on`
- `file_hints`
- `done_when`
- `verify.commands`
- `manual_gate`
- `behavior_invariants`
- `blocked_by_visual_review`

### 9.3 `done_when` 要求

不得写成“更美观”“更高级”这类主观描述，必须写成可收口条件，例如：

- 某一类控件全部切到新样式族
- 某一组窗口全部切到新 shell
- owner/topmost/focus/keyboard 行为基线未回归
- 指定人工 gate 已通过

## 10. UI State 设计

`.codex/ui-window-system-state.json` 建议比现有 refactor state 多这些字段：

- `mode`
- `mode_family`
- `visual_phase`
- `current_manual_gate`
- `theme_frozen`
- `main_scene_frozen`
- `fullscreen_frozen`
- `final_visual_review_passed`
- `governing_reconciliation`

此外保留标准执行字段：

- `current_task`
- `tasks`
- `blocked`
- `history`

其中：

- `mode = "ui-window-system"`
- `mode_family = "ui-overhaul"`
- `governing_reconciliation` 至少记录 `last_reconciled_at`、`doc_paths`、`doc_sync_policy`

## 11. 人工 Gate 规则

UI mode 必须支持硬性人工 gate。建议首批定义：

- `theme-freeze`
- `main-scene-freeze`
- `fullscreen-float-freeze`
- `final-visual-regression`

规则：

- 命中 gate 必须停
- 不允许越过 gate 自动推进下一大阶段
- 状态应输出 `BLOCKED_NEEDS_HUMAN` 或等价状态
- wrapper 需要把当前 gate 明确打印出来

### 11.1 Gate 解锁协议

人工 gate 通过后，必须存在明确恢复协议：

1. 人工确认人更新对应 governing doc 证据
   - 例如 progress/acceptance 文档写入确认结论
2. wrapper 或 repo-local helper 显式执行 gate resume
3. gate resume 写回：
   - `theme_frozen` / `main_scene_frozen` / `fullscreen_frozen` / `final_visual_review_passed`
   - 清理对应 `blocked_by_visual_review`
   - 记录 history
   - 清空 `current_manual_gate`
4. loop 下次迭代从已解锁 gate 之后继续

约束：

- 不允许靠人工直接改 JSON 且不留文档证据
- gate 通过证据必须落到 authoritative governing docs 中
- repo-local adapter 负责提供 resume helper，不由 generic loop 直接写 UI gate 细节

补充状态写回要求：

- 对应任务若此前因视觉 gate 被标为 `blocked`，resume 时必须恢复为 `pending`
- 必须清理该任务在 `state.blocked` 中的对应阻塞记录
- 不直接恢复为 `in_progress`；由下一轮正常选题重新 claim
- history 中必须记录 `gate-resume` 动作与证据文档路径

## 12. 验证链

### 12.1 自动验证

建议至少包含：

- `dotnet build`
- 受影响测试过滤
- 资源解析与 XAML 编译检查
- 关键窗口协调测试

### 12.1.1 自动回归映射

repo-local adapter 需要把现有辅助脚本与 mode-aware 行为绑定清楚：

- `select-next-task.ps1`
  - 改成支持按 `mode_id` 读取对应 `tasks_file` / `state_file`
- `update-refactor-state.ps1`
  - 泛化或新增 mode-aware 版本，支持写入 `ui-window-system-state.json`
- `check-doc-consistency.ps1`
  - 改成按 mode 读取 authoritative governing docs
- `test-governing-reconciliation.ps1`
  - 改成按 mode 检查 reconciliation
- `scripts/run-refactor-loop.ps1`
  - 增加 `-Mode`
  - 由 mode registry 解析 `TaskFile`、`StateFile`、`ConfigFile`
- 日志目录
  - 建议按 mode 拆分，例如 `.codex/logs/refactor-loop/ui-window-system/`

reconciliation 刷新写回协议：

- 当人工 gate 通过且 authoritative docs 已更新后，repo-local adapter 必须执行一次 mode-aware reconciliation refresh
- refresh 负责写回：
  - `governing_reconciliation.last_reconciled_at`
  - `governing_reconciliation.doc_paths`
  - `governing_reconciliation.doc_sync_policy`
- 只有在 refresh 成功后，后续迭代才允许把 mode 推向 `ALL_AUTOMATABLE_TASKS_DONE`

### 12.2 人工验证

必须覆盖：

- 主窗口观感
- 点名/计时远距可读性
- 工具条紧凑度
- 全屏内容优先
- 设置窗层级
- DPI/跨屏/投影
- owner/topmost/focus/keyboard 行为

### 12.3 冻结验证

冻结阶段建议单独记录：

- `theme-freeze`
- `main-scene-freeze`
- `fullscreen-float-freeze`
- `final-visual-regression`

## 13. 停止条件

建议 UI mode 使用以下停止条件：

- `ALL_AUTOMATABLE_TASKS_DONE`
  - 自动任务已完成，且所有人工 gate 已通过
- `BLOCKED_NEEDS_HUMAN`
  - 命中人工视觉 gate
  - 缺少 governing doc
  - 缺少视觉确认
- `NO_ELIGIBLE_TASK`
  - 当前没有可自动执行的 UI 任务

补充：

- `ALL_AUTOMATABLE_TASKS_DONE` 必须同时满足：
  - UI tasks 全部完成
  - 所有 manual gates 已通过
  - governing reconciliation 通过

## 14. Wrapper 行为

wrapper 需要支持显式 mode 参数，例如：

- `-Mode architecture-refactor`
- `-Mode ui-window-system`

UI mode 下，wrapper 还需要：

- 输出当前阶段
- 输出当前 manual gate
- 输出下一窗口范围
- 命中 gate 时立即停止

### 14.1 lock / ownership 协议

多 mode 下建议使用“共享锁文件 + 记录 `mode_id`”方案，而不是 mode 独立锁。

建议继续使用：

- `.codex/refactor-loop.lock.json`

但锁记录至少增加：

- `owner_kind`
- `pid`
- `loop_run_id`
- `started_at`
- `mode_id`
- `mode_family`
- `state_file`
- `task_file`
- `config_file`

理由：

- 同一仓库同一时间只允许一个 repo-local loop writer
- 避免 `architecture-refactor` 与 `ui-window-system` 并发写不同状态文件却共享工作区
- 避免 mode 独立锁导致的“互不阻塞但实际冲突”

规则：

- 任一 mode 持锁时，其他 mode 必须阻塞
- resume 时必须校验锁中的 `mode_id`
- wrapper 和 child iteration 都要把 `mode_id` 作为 ownership 语义的一部分
- 必须保留现有 same-loop ownership 识别字段，避免 wrapper-owned child 被误判为外部并发执行器

## 15. 技能优化优先级

### 第一优先：`autonomous-refactor-loop`

先把它从“单一架构迁移 loop”升级为“repo-local multi-mode adapter”。

最少需要支持：

- mode registry
- mode 路由
- mode 专属 tasks/state/config
- mode 专属 governing docs
- mode 专属 verify/gate/stop rules

### 第二优先：`autonomous-execution-loop`

generic 层后续可补：

- `ui-overhaul` 抽象 family
- mode-aware compatibility bridge
- 更明确的 family -> repo-local mode 绑定

但这一项是第二阶段、system-owned follow-up，不纳入本次 repo-local implementation pass

## 16. 实施顺序

建议后续按以下顺序落地：

1. 完成 UI/window spec 最终收口
2. 写出 UI mode implementation plan
3. 改 repo-local `autonomous-refactor-loop` 支持 mode registry
4. 新建 UI mode tasks/state/config
5. 完成 repo-local 验证与 dry-run
6. 如有需要，再单独立项改 generic `autonomous-execution-loop` 的 mode-aware bridge
7. 启动 UI 自动循环

### 16.1 最小 schema 示例

后续 implementation plan 应至少给出 3 份最小 schema 示例：

- `mode-registry.json`
- `ui-window-system-state.json`
- `refactor-loop.lock.json`（含 `mode_id`）

## 17. 结论

本设计不建议把 `ui-window-system` 直接塞进 generic kernel，也不建议继续让当前 repo-local loop 只服务于架构迁移。

最佳边界是：

- generic loop 持有“怎么跑”的通用协议
- repo-local loop 持有“这个仓库跑什么”的 mode 映射
- UI mode 持有“这次终态外观重构做哪些任务”的执行图

只有这样，两个技能的边界才清晰，自动化也才不会跑错目标。

