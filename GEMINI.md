# GEMINI.md — ClassroomToolkit（Gemini 项目级）
**项目**: ClassroomToolkit  
**类型**: Windows WPF (.NET 10)  
**适用范围**: 项目级（仓库根）  
**版本**: 3.95
**最后更新**: 2026-04-27

## 1. 阅读指引（必读）
- 本文件承接 `GlobalUser/GEMINI.md v9.44`，仅定义本仓落地动作（WHERE/HOW）。
- 固定结构：`1 / A / B / C / D`。
- 裁决链：`运行事实/代码 > 项目级文件 > 全局文件 > 临时上下文`。
- 自包含约束：执行规则以本文件正文为准，不依赖外部子文档或治理脚本作为前置条件。
- 渐进披露边界：根文件必须保留本仓边界、门禁、阻断、证据和回滚；长 runbook、示例和历史背景可下沉到子文档，但不得成为执行前置条件。
- 精简原则：根文件只写课堂场景硬边界、真实门禁、证据与回滚；长 UX 清单、触屏验收细节和历史问题放入 `docs/` 或按需 imports。

## A. 共性基线（仅本仓）
### A.1 项目不变约束
- 课堂可用性优先：不可崩溃、不可长时间卡死，外部依赖失败必须可降级。
- Interop 防护：`Win32/COM/WPS/UIAutomation` 异常不得冒泡到 UI。
- 兼容保护：不得破坏 `students.xlsx`、`student_photos/`、`settings.ini` 的格式与语义。

### A.2 执行锚点
- 每次改动先声明：边界 -> 当前落点 -> 目标归宿。
- 默认中文沟通、中文解释、中文汇报；代码标识符、命令、日志、报错和协议字段保留英文原文。
- 全局规则给风险、语言、N/A 和门禁语义；本文件给 ClassroomToolkit 的模块边界、真实命令、课堂场景不变量、证据与回滚入口。
- 项目规则只保留本仓不可由代码/CI自动推断且会改变执行、风险或验收的事实；长流程下沉到子文档或工具专属规则。
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

## B. Gemini 平台差异（项目内）
### B.1 加载与覆盖
- 用户规则：`~/.gemini/GEMINI.md`；项目/工作区规则按 Gemini CLI 层级加载和按需发现执行。
- 启用 Trusted Folders 时，未受信目录可能进入 safe mode；遇到项目配置、环境变量、自动记忆或工具自动批准未生效，先记录 trust 状态或替代证据。
- 可用 `@file.md` imports 组织长内容；只有本机 `settings.json` 明确配置 `context.fileName` 时，才把其他文件名视为 Gemini 上下文文件。
- 用 `.geminiignore` 排除 `bin/`、`obj/`、测试结果、日志和本机敏感配置；修改后用 `/memory show` 核查完整上下文；来源与刷新命令先看当前 `/memory` help，支持则用 `/memory list` / `/memory refresh`，否则记录版本并用 `/memory reload` 兜底。
- 不假定 `GEMINI.override.md` 存在；临时排障规则必须记录清理点，结论后删除或恢复并复测。

### B.2 最小诊断矩阵
- 必做：`gemini --version`、`gemini --help`。
- 状态/诊断命令采用“help 探测 -> 有则执行 -> 无则 `platform_na` 落证”；交互场景用 `/memory show` 查完整上下文；来源与刷新命令先看当前 `/memory` help，支持则用 `/memory list` / `/memory refresh`，否则记录版本并用 `/memory reload` 兜底；非交互不可用时按 `platform_na` 记录。
- 留痕最低字段：`cmd`、`exit_code`、`key_output`、`timestamp`。

### B.3 平台异常回退
- 命令缺失或行为不一致时，必须记录：`platform_na/gate_na`、原因、替代命令、证据位置。
- `GEMINI.md` 是上下文规则；确定性验证、安全拦截和回滚能力应落到本仓门禁、MCP/扩展、checkpoint/restore 或 CI。
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
  - `R6`: 本仓门禁命令是硬门禁；quick/fast 只能作为已声明的日常反馈切片，交付前仍按 full gate 或固定顺序收口。
  - `R8`: 证据与回滚字段是最小留痕；缺字段必须按 N/A 口径说明。
  - `E4`: hotspot 复核课堂可用性、Interop 防护、触屏/窗口层级和设置加载边界。
  - `E5`: NuGet/.NET/系统组件变化必须记录依赖与平台基线；新增生产依赖前先说明必要性。
  - `E6`: `students.xlsx`、`settings.ini`、照片目录或持久化结构变化必须记录兼容性、迁移和回滚。
- 子文档只承载细节，不替代根文件中的硬门禁和项目事实。
- 三文件同构约束：`A/C/D` 必须语义一致，仅 `B` 允许平台差异。
