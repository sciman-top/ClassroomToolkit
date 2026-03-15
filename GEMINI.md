# GEMINI.md — ClassroomToolkit 项目规则（Gemini CLI）
**项目**: ClassroomToolkit  
**类型**: Windows WPF (.NET 10)  
**适用范围**: 项目级（仓库根）  
**上下文**: 项目根目录  
**版本**: 1.50  
**最后更新**: 2026-03-15

## 0. 变更记录
- 2026-03-15 v1.50：按六文件协同原则重构项目规则；压缩冗余；强化终态架构归宿判定与反堆叠约束。
- 2026-03-14 v1.45：补充终态蓝图扩展守则与热点文件限制。
- 2026-02-13 v1.43：补充项目级边界判定矩阵与 B 节回滚提示模板。

## 1. 阅读指引（项目级）
- 本文件聚焦仓库特性与验证落地；全局共性与平台机制见 `GlobalUser/对应同名文件`。
- 三层协同模型：共性基线（项目）+ 平台差异（项目）+ 项目差异（领域）。
- 项目规则优先于全局规则；冲突时需在回复中写明采用依据。

## A. 共性基线（项目级）
- 稳定性优先：课堂场景不得崩溃或长时间卡死，外部交互失败必须有降级路径。
- Interop 防御：Win32/COM 异常不得冒泡到 UI 层。
- 数据边界：不得破坏 `students.xlsx`、`student_photos/`、`settings.ini` 结构与兼容性。
- 语言边界：对用户默认中文；代码、命令、日志保留英文。

### A.1 协作与优先级
- 规则来源：`GlobalUser/对应同名文件` -> 仓库同名文件，以项目级为准。
- 项目级职责：仓库结构、领域约束、验证与回滚。
- 全局级职责：跨项目共性基线、平台加载语义与维护方法。
- 目标是 1+1>2：互补不重复、边界清晰、直接可执行。

### A.2 操作与验证范式
- 检索优先 `rg`/`rg --files`，仅打开必要文件。
- 批量改动定义：>=2 文件或跨模块，完成后必须说明影响面。
- 新增功能、重构、缺陷修复都要先判定归宿层：`App/View`、`Session`、`Windowing`、`Application`、`Domain`、`Infra`、`Interop`、`Services`。
- 未完成归宿判定前，不得把规则、状态写回、外部交互分支直接堆入热点窗口文件或 `Services`。
- 多副本目录场景下，执行前先确认当前工作目录。

### A.3 边界判定矩阵（项目执行）
- 仅改项目：仓库目录、领域规则、测试发布命令、数据边界、UI/Interop 风险策略。
- 仅改全局：跨项目稳定规则、平台加载配置诊断、统一评分口径。
- 两层联动：当项目实践反向影响全局模板时，先更新 GlobalUser，再回写项目最小复述与验证动作。

## B. 平台差异（Gemini CLI 项目内）
### B.1 加载与作用域
- 最小摘要：默认使用 `GEMINI.md`，目录层级与 ignore 规则共同影响加载。
- 操作：规则未生效时先检查 `.geminiignore` 与工作目录层级。
- 回滚：任务后回退临时 ignore 或临时上下文文件名。

### B.2 配置与上限
- 最小摘要：context discovery 相关上限与字段以官方文档为准。
- 操作：改配置前后执行 memory 刷新并记录差异。
- 回滚：试验配置完成后恢复基线值。

### B.3 诊断与命令
- 最小摘要：优先通过 memory 命令确认当前上下文。
- 操作：若发现上下文偏差，先刷新再修正规则文件。
- 回滚：清理排查期加入的临时记忆。

### B.4 注意事项
- 小改优先 `apply_patch`，跨多文件批改优先脚本化，便于复核。
- 本节只保留平台在本仓库的差异，不复制全局通用条款。

## C. 项目差异（领域/技术）
### 1. 目录与模块
- `src/ClassroomToolkit.App`：WPF UI、MainViewModel、启动 DI。
- `src/ClassroomToolkit.Domain`：核心业务规则。
- `src/ClassroomToolkit.Services`：应用服务编排，不承接核心业务规则。
- `src/ClassroomToolkit.Interop`：Win32/COM 高风险封装。
- `src/ClassroomToolkit.Infra`：配置与持久化。
- `src/ClassroomToolkit.App/Windowing`：多窗口编排策略。
- `tests/ClassroomToolkit.Tests`：xUnit + FluentAssertions。

### 2. 构建/测试/运行
- `dotnet restore`
- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet run --project src/ClassroomToolkit.App/ClassroomToolkit.App.csproj`
- `dotnet build ClassroomToolkit.sln -c Release`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Release`
- `powershell -File scripts/ctoolkit.ps1`（可加 `-SkipTests`/`-SkipCommit`）

### 3. 代码规范
- 4 spaces 缩进、file scoped namespaces。
- 类型与方法 PascalCase，局部变量 camelCase，接口 `I` 前缀。
- Nullability 显式声明；不可变模型优先 record。
- 注释写 why，不重复 what。

### 4. UI 开发（WPF）
- 样式与颜色使用资源字典，避免硬编码。
- 新窗口继承 `BaseWindow` 或保持现有视觉体系。

### 5. 变更边界与依赖
- 禁止修改示例数据结构与配置格式兼容性。
- 禁止向 `MainWindow.*`、`PaintOverlayWindow.*`、`RollCallWindow.*` 继续堆业务编排。
- 禁止把 `Services` 扩张为第二业务中心；业务规则优先回归 Application/Domain/Infra。
- Interop 大规模改造前需先评估风险与降级路径。

### 6. 关键技术要点（高风险区）
- COM 对象必须释放，异常必须降级。
- 处理 `RPC_E_CALL_REJECTED` 场景并提供重试或退化策略。
- WPS 兼容优先 `KWPP.Application`，回退 `WPP.Application`。
- High DPI 跨屏场景必须人工验收。

### 7. 调试与技术债
- 调试标签：`[WpsTracker]`、`[InkCache]`、`[PresentationControl]`、`[UIAutomation]`。
- 技术债重点：输入拦截、Ink 性能、WPS COM 重试、DPI 清晰度。

### 8. 验证与测试
- 轻量验证可按类过滤，完整验证跑 Debug 全量测试。
- Interop 难单测时，优先补 Domain/Application 的确定性测试。
- 未执行测试需在回复中说明原因、风险和回滚方案。

### 9. 变更影响模板
- 模板：`影响模块=；影响数据/配置=；UI/交互=；Interop/外部依赖=；验证与回滚=`。
- 规则改写与多文件改动必须填写。

### 10. 提交与 PR
- Commit 使用中文简要摘要，可带 `feat:` `fix:` `refactor:` 前缀。
- PR 至少包含：摘要、验证命令、UI 变更截图（如有）。

## D. 维护校验清单（项目级）
说明：维护清单为附录，不属于三层结构正文。
- 保持 1/A/B/C/D 结构与编号一致。
- 项目三文件 A/C/D 必须一致，仅 B 节允许平台差异。
- B 节需同时具备最小摘要、操作动作、风险或回滚提示。
- 复核 GlobalUser 引用链和语义一致性。
- 复核终态架构约束是否仍可执行，避免规则漂移成口号。
- 更新版本号、日期、变更记录并自检冲突。

