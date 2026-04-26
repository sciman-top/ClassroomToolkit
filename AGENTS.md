# AGENTS.md — ClassroomToolkit（Codex 项目级）
**项目**: ClassroomToolkit  
**类型**: Windows WPF (.NET 10)  
**适用范围**: 项目级（仓库根）  
**版本**: 3.94
**最后更新**: 2026-04-26

## 1. 阅读指引（必读）
- 本文件承接 `GlobalUser/AGENTS.md v9.43`，仅定义本仓落地动作（WHERE/HOW）。
- 固定结构：`1 / A / B / C / D`。
- 裁决链：`运行事实/代码 > 项目级文件 > 全局文件 > 临时上下文`。
- 自包含约束：执行规则以本文件正文为准，不依赖外部子文档或治理脚本作为前置条件。
- 渐进披露边界：根文件必须保留本仓边界、门禁、阻断、证据和回滚；长 runbook、示例和历史背景可下沉到子文档，但不得成为执行前置条件。
- 精简原则：根文件只写课堂场景硬边界、真实门禁、证据与回滚；长 UX 清单、触屏验收细节和历史问题放入 `docs/`。

## A. 共性基线（仅本仓）
### A.1 项目不变约束
- 课堂可用性优先：不可崩溃、不可长时间卡死，外部依赖失败必须可降级。
- Interop 防护：`Win32/COM/WPS/UIAutomation` 异常不得冒泡到 UI。
- 兼容保护：不得破坏 `students.xlsx`、`student_photos/`、`settings.ini` 的格式与语义。

### A.2 执行锚点
- 每次改动先声明：边界 -> 当前落点 -> 目标归宿。
- 默认中文沟通、中文解释、中文汇报；代码标识符、命令、日志、报错和协议字段保留英文原文。
- 全局规则给风险、语言、N/A 和门禁语义；本文件给 ClassroomToolkit 的模块边界、真实命令、课堂场景不变量、证据与回滚入口。
- 小步闭环，优先根因修复；止血补丁必须标明回收时点。
- 每次变更留痕：`依据 -> 命令 -> 证据 -> 回滚`。

### A.3 N/A 分类与字段（项目内）
- `platform_na`：平台能力缺失、命令不存在或非交互限制导致命令不可用。
- `gate_na`：门禁步骤客观不可执行（含脚本缺失、测试子集过滤不可执行、纯文档/注释/排版改动）。
- 两类 N/A 均必须记录：`reason`、`alternative_verification`、`evidence_link`、`expires_at`。
- N/A 不得改变门禁顺序：`build -> test -> contract/invariant -> hotspot`。

### A.4 触发式澄清协议（本仓）
- 默认执行：`direct_fix`（先修复、后验证）。
- 触发条件：同一 `issue_id` 连续失败达到阈值（默认 `2`），或现象/期望持续冲突。
- 澄清上限：一次最多 3 个高价值问题；确认后恢复 `direct_fix` 并清零失败计数。
- 留痕字段：`issue_id`、`attempt_count`、`clarification_mode`、`clarification_questions`、`clarification_answers`。

## B. Codex 平台差异（项目内）
### B.1 加载与覆盖
- 目录：`~/.codex`（可由 `CODEX_HOME` 覆盖）。
- 项目链从 Git root 到当前目录逐层加载；同层优先级：`AGENTS.override.md > AGENTS.md > configured fallback`。
- override 仅用于短期排障，结论后必须清理并复测。

### B.2 最小诊断矩阵
- 必做：`codex --version`、`codex --help`。
- 状态检查优先 `codex status`；非交互失败（如 `stdin is not a terminal`）时按 `platform_na` 落证。
- 留痕最低字段：`cmd`、`exit_code`、`key_output`、`timestamp`。

### B.3 平台异常回退
- 命令缺失或行为不一致时，必须记录：`platform_na/gate_na`、原因、替代命令、证据位置。
- `AGENTS.md` 是上下文规则；确定性验证、权限或安全拦截应落到本仓门禁、hooks、CI 或管理脚本。
- 替代命令仅用于补证据，不得改变门禁顺序与阻断语义。

## C. 项目差异（领域与技术）
### C.1 模块边界
- `src/ClassroomToolkit.App`：WPF UI 与启动编排。
- `src/ClassroomToolkit.Application`：用例编排与应用服务。
- `src/ClassroomToolkit.Domain`：核心业务规则。
- `src/ClassroomToolkit.Services`：应用层桥接。
- `src/ClassroomToolkit.Interop`：高风险系统接口封装。
- `src/ClassroomToolkit.Infra`：配置、存储、文件系统与外部资源。
- `tests/ClassroomToolkit.Tests`：回归与契约测试。

### C.2 门禁命令与顺序（硬门禁）
- build：`dotnet build ClassroomToolkit.sln -c Debug`
- test：`dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- contract/invariant：
  `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- hotspot：对本次改动文件执行热点人工复核并记录结论。
- fixed order：`build -> test -> contract/invariant -> hotspot`

### C.3 失败分流与阻断
- build 失败：阻断，先修编译错误和引用断裂。
- test 失败：阻断，先修回归失败再重跑全链路。
- contract/invariant 失败：高风险阻断，禁止合并或发布。
- hotspot 未收敛：阻断；如无法执行则按 `gate_na` 落证并补替代验证。

### C.4 证据与回滚
- 证据目录：`docs/change-evidence/`。
- 建议命名：`YYYYMMDD-topic.md`。
- 最低字段：规则 ID、风险等级、执行命令、关键输出、回滚动作。

### C.5 CI 与本地入口
- 本地门禁以 C.2 命令为准。
- CI 入口以仓库现有配置为准（当前可见：`azure-pipelines.yml`、`.gitlab-ci.yml`）。

### C.6 Git 提交与推送边界（“全部”定义）
- `整理提交全部` 的“全部”仅指：`本次任务相关 + 应被版本管理 + 通过 .gitignore 的文件`。
- 默认不纳入“全部”：IDE/agent 本地配置、临时文件、日志、缓存与本地运行态目录。
- `push` 仅推送既有 commit 历史；文件筛选必须在 `git add/commit` 前完成。

## D. 维护校验清单（项目级）
- 仅落地本仓事实，不复述全局规则正文。
- 与全局职责互补，不重叠、不缺失。
- 协同链完整：`规则 -> 落点 -> 命令 -> 证据 -> 回滚`。
- `Global Rule -> Repo Action`：
  - `E4`: hotspot 复核课堂可用性、Interop 防护、触屏/窗口层级和设置加载边界。
  - `E5`: NuGet/.NET/系统组件变化必须记录依赖与平台基线；新增生产依赖前先说明必要性。
  - `E6`: `students.xlsx`、`settings.ini`、照片目录或持久化结构变化必须记录兼容性、迁移和回滚。
- 子文档只承载细节，不替代根文件中的硬门禁和项目事实。
- 三文件同构约束：`A/C/D` 必须语义一致，仅 `B` 允许平台差异。
