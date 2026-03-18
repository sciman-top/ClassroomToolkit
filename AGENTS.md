# AGENTS.md — ClassroomToolkit 项目规则（Codex）
**项目**: ClassroomToolkit  
**类型**: Windows WPF (.NET 10)  
**适用范围**: 项目级（仓库根）  
**上下文**: 项目根目录  
**版本**: 1.85  
**最后更新**: 2026-03-18

## 0. 变更记录
- 2026-03-18 v1.85：统一升版项目级三文件；增强“终态方案先行”执行模板；补强全局-项目协同交付闭环，明确职责不重叠不缺失。
- 2026-03-18 v1.84：强化终态方案在新建/扩展/修复/重构全场景触发；补全项目-全局协同交付物；完善变更影响模板中的当前落点与目标归宿字段。
- 2026-03-15 v1.83：强化“全局-项目协同接口 + 三层边界矩阵 + 终态与反堆叠闸门”一致性；优化项目自包含表达并压缩重复描述。

## 1. 阅读指引（项目级）
- 根目录 `AGENTS.md`、`CLAUDE.md`、`GEMINI.md` 为项目级；`GlobalUser/` 同名文件为全局用户级。
- 本文件聚焦 ClassroomToolkit 仓库落地，不复写全局通用条款。
- 三层固定：共性基线（A）+ 平台差异（B）+ 项目差异（C）；D 为维护附录。
- 本文件必须单独可读：不打开全局文件也能执行本仓库任务。
- 若与全局冲突，以项目级为准，并在回复中说明采用依据。

## A. 共性基线（项目级）
- 稳定性优先：课堂场景不得崩溃或长时间卡死，外部交互失败必须有降级路径。
- Interop 防御：Win32/COM 异常不得冒泡到 UI 层，必须在边界层拦截并降级。
- 数据边界：不得破坏 `students.xlsx`、`student_photos/`、`settings.ini` 结构与兼容性。
- 默认中文沟通；代码、命令、日志、错误原文保留英文。
- 默认仅单条最终回复；仅在真实阻塞或不可逆高风险操作前允许额外消息。

### A.1 协同接口与职责边界
- 本文件负责把 `GlobalUser/对应同名文件` 的全局硬约束落成仓库动作、目录落点、验证命令与回滚方式。
- 项目级职责：仓库结构、领域边界、终态落点、构建测试发布、风险降级、验证回滚。
- 全局级职责：跨项目共性基线、平台加载语义、统一评分口径、维护同步方法。
- 协同目标：全局定规则，项目给落点，做到功能不重叠、不缺失（`1 + 1 > 2`）。

### A.2 终态方案先行（新建/扩展/修复/重构）
- 触发场景：新建模块、新建子系统、扩展功能、新增能力、性能优化、错误修复、重构。
- 动手前先输出“需求边界 -> 技术栈 -> 目标架构 -> 分阶段迁移”方案，先定终态再定实现。
- 需求边界至少覆盖：课堂场景目标、角色流程、性能与稳定性指标、依赖约束、兼容边界、明确 out-of-scope。
- 方案至少覆盖：目标模块、依赖方向、状态归属、数据边界、验证策略、迁移步骤、回滚路径。
- 目标必须对齐最佳实践终态：模块化、结构化、规范化、便于长期维护。
- 若现状与终态不一致，必须同时写清“当前落点、目标归宿、过渡接线方式、阶段性回滚点”。

### A.3 归宿判定（扩展/修复前必做）
- UI 展示、命令绑定、视觉状态归 `App/View`。
- 会话态、课堂流程态、运行期协作状态归 `Session` 或等价模块。
- 多窗口协调、焦点切换、窗口生命周期归 `Windowing`。
- 应用用例编排、跨模块流程协调归 `Application`；若暂未独立抽象，不得回堆窗口。
- 核心业务规则与纯领域判断归 `Domain`。
- 配置、持久化、缓存、文件系统等细节归 `Infra`。
- Win32/COM/WPS/UIAutomation 等高风险边界归 `Interop`。
- `Services` 仅承接应用服务桥接与编排，不得演化为第二业务中心。

### A.4 反堆叠执行闸门（扩展/新增/修复）
- 未完成归宿判定前，不得把业务规则、状态写回、外部交互分支、临时补丁堆入窗口代码、`Services`、工具类或热点文件。
- 命中 `MainWindow.*`、`PaintOverlayWindow.*`、`RollCallWindow.*` 时，优先抽离逻辑到目标模块后再接入 UI。
- 紧急止血允许先做，但必须同步说明最终归宿；禁止把临时兼容沉淀为长期结构债。
- 新增能力必须服从目标架构收口，不得为局部速度破坏整体可维护性。

### A.5 操作、验证与回滚范式
- 检索优先 `rg` / `rg --files`，只打开必要文件。
- 批量改动定义为 `>=2` 文件或跨模块；完成后必须说明影响模块、风险点与回滚方式。
- 多副本目录场景下，执行前先确认当前工作目录，防止误改。
- 无法执行验证时，必须明确：未验证项、潜在风险、建议补测、回滚方案。

### A.6 边界判定矩阵（项目执行）
- 仅改项目：仓库目录、领域规则、测试发布命令、数据边界、UI/Interop 风险策略、热点区限制。
- 仅改全局：跨项目稳定规则、平台加载配置诊断语义、统一评分口径、维护同步策略。
- 两层联动：项目实践暴露全局模板缺口时，先更新 GlobalUser，再回写项目最小复述与仓库动作。
- 协同交付链必须闭环：`规则判定 -> 目录落点 -> 执行命令 -> 验证证据 -> 回滚动作`。

## B. 平台差异（Codex 项目内）
### B.1 加载与作用域
- 最小摘要：优先级为 `AGENTS.override.md` > `AGENTS.md` > fallback。
- 操作：规则生效异常时，先核对当前目录、激活作用域与全局加载链说明。
- 回滚：临时 override 完成后删除，并复测仓库是否恢复默认加载链。

### B.2 配置与上限
- 最小摘要：常见配置文件位于 `~/.codex/config.toml`。
- 操作：调整上限与 fallback 前先核对官方文档字段，再记录修改目的、验证方式与回滚点。
- 回滚：试验结束后恢复基线值，并再次验证规则发现结果。

### B.3 诊断与命令
- 最小摘要：优先使用 `codex status` 与非交互命令做规则诊断。
- 操作：先诊断作用域与激活规则，再修正规则文件，避免盲改。
- 回滚：清理排查期引入的临时配置、临时覆盖文件与临时说明。

### B.4 注意事项
- 小改优先 `apply_patch`，跨多文件批改优先脚本化，便于复核。
- 本节仅保留 Codex 在本仓库的差异，不复制全局共性条款。

## C. 项目差异（领域/技术）
### 1. 目录与模块
- `src/ClassroomToolkit.App`：WPF UI、MainViewModel、启动 DI。
- `src/ClassroomToolkit.Application`：应用用例编排、跨模块流程协调、面向 UI 的应用层边界。
- `src/ClassroomToolkit.Domain`：核心业务规则。
- `src/ClassroomToolkit.Services`：应用服务桥接与编排，不承接核心业务规则。
- `src/ClassroomToolkit.Interop`：Win32/COM/WPS/UIAutomation 高风险封装。
- `src/ClassroomToolkit.Infra`：配置、持久化、文件系统与基础设施细节。
- `src/ClassroomToolkit.App/Windowing`：多窗口编排策略。
- `tests/ClassroomToolkit.Tests`：xUnit + FluentAssertions。

### 2. 构建 / 测试 / 运行
- `dotnet restore`
- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet run --project src/ClassroomToolkit.App/ClassroomToolkit.App.csproj`
- `dotnet build ClassroomToolkit.sln -c Release`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Release`
- `powershell -File scripts/ctoolkit.ps1`（可加 `-SkipTests` / `-SkipCommit`）

### 3. 代码规范
- 4 spaces 缩进、file scoped namespaces。
- 类型与方法 PascalCase，局部变量 camelCase，接口 `I` 前缀。
- Nullability 显式声明；不可变模型优先 record。
- 注释写 why，不重复 what。

### 4. UI 开发（WPF）
- 样式、颜色、尺寸使用资源字典，避免硬编码。
- 新窗口优先继承 `BaseWindow` 或保持现有视觉体系。
- UI 层只负责展示、交互与绑定，不承载跨模块业务编排。

### 5. 变更边界与依赖
- 禁止修改示例数据结构与配置格式兼容性。
- 禁止向 `MainWindow.*`、`PaintOverlayWindow.*`、`RollCallWindow.*` 继续堆业务编排。
- 禁止把 `Services` 扩张为第二业务中心；业务规则优先回归 `Application` / `Domain` / `Infra`。
- Interop 大规模改造前必须先评估风险、降级路径与人工验收范围。

### 6. 关键技术要点（高风险区）
- COM 对象必须释放，异常必须降级。
- 必须处理 `RPC_E_CALL_REJECTED` 场景，并提供重试或退化策略。
- WPS 兼容优先 `KWPP.Application`，回退 `WPP.Application`。
- High DPI 跨屏场景必须人工验收。

### 7. 调试与技术债
- 调试标签：`[WpsTracker]`、`[InkCache]`、`[PresentationControl]`、`[UIAutomation]`。
- 技术债重点：输入拦截、Ink 性能、WPS COM 重试、DPI 清晰度。
- 修复技术债时，优先把逻辑收回目标模块，不新增临时绕路层。

### 8. 验证与测试
- 轻量验证可按类过滤；完整验证优先跑 Debug 全量测试。
- Interop 难单测时，优先补 `Domain` / `Application` 的确定性测试。
- 未执行测试时，回复必须说明原因、风险与回滚方案。

### 9. 变更影响模板
- 模板：`影响模块=；当前落点=；目标归宿=；影响数据/配置=；UI/交互=；Interop/外部依赖=；验证与回滚=`。
- 规则改写、批量改动、跨模块调整必须填写。

### 10. 提交与 PR
- Commit 使用中文简要摘要，可带 `feat:`、`fix:`、`refactor:` 前缀。
- PR 至少包含：摘要、验证命令、UI 变更截图（如有）、风险与回滚说明。

## D. 维护校验清单（项目级）
说明：维护清单为附录，不属于三层结构正文。
- 保持 `1 / A / B / C / D` 结构与编号一致。
- 项目三文件 A/C/D 必须一致，仅 B 节允许平台差异。
- B 节必须同时具备：最小摘要、操作动作、风险或回滚提示。
- 复核与 `GlobalUser/对应同名文件` 的引用链与语义一致性。
- 复核终态架构要求能映射到当前目录与热点文件，避免口号化。
- 复核精简度：删重复、补动作、留回滚，不做无意义膨胀。
- 版本号、日期、变更记录与六文件协同口径必须同步。
