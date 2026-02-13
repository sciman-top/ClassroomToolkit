# GEMINI.md — ClassroomToolkit 项目规则（Gemini CLI）
**项目**: ClassroomToolkit  
**类型**: Windows WPF (.NET 8)  
**适用范围**: 项目级（仓库根）  
**上下文**: 项目根目录  
**版本**: 1.42  
**最后更新**: 2026-02-13

## 0. 变更记录
- 2026-02-13 v1.42：同步架构改造落地（DI 启动链、MainViewModel、Windowing 协调器、PerMonitorV2、新增测试与 Release 验证命令）。
- 2026-01-25 v1.41：移除首句模板要求，保持中文默认优先。
- 2026-01-25 v1.40：平台差异模板化为“最小摘要/操作/风险提示”；说明触发移至 A.2。
- 2026-01-25 v1.39：补充阅读指引；修正变更记录表述以匹配实际修改。
- 2026-01-25 v1.38：调整首句模板触发条件；补充最小复述超限说明；补充 .geminiignore 提示。
- 2026-01-25 v1.37：优化 Gemini 平台差异条目，去除重复表述并收敛注意事项。
- 2026-01-25 v1.36：强化中文回复硬性要求；调整最小复述约束口径；补充多副本目录确认到 A.2 并清理平台差异。
- 2026-01-25 v1.35：进一步压缩 B 节最小摘要为符号化动词句并同步三文件。
- 2026-01-25 v1.34：进一步压缩 B 节最小摘要为关键动词句并同步三文件。
- 2026-01-25 v1.33：统一 B 节操作行为简短模板并同步三文件。
- 2026-01-25 v1.32：B 节收敛为“1 条最小摘要 + 1 条操作动作”并同步三文件。
- 2026-01-25 v1.31：压缩并增强 B 节最小摘要可执行性，同步三文件。
- 2026-01-25 v1.30：统一自包含边界口径为单句合并表述并同步三文件。
- 2026-01-25 v1.29：补充项目级 B 节最小复述并调整自包含边界表述，同步三文件。
- 2026-01-25 v1.28：补充中文回复首句声明模板并同步三文件。
- 2026-01-25 v1.27：强化中文沟通口径；扩展 Gemini 项目内平台差异条目。
- 2026-01-25 v1.26：补充 Gemini 上下文刷新实践条目。
- 2026-01-25 v1.25：补充 Gemini 项目内平台实践差异条目。
- 2026-01-25 v1.24：B.1-B.3 增加指向 GlobalUser 对应小节的继承锚点。
- 2026-01-24 v1.23：统一 A.1 规则来源表述，确保项目级共性段一致。
- 2026-01-24 v1.22：补充项目级一致性约束，明确仅平台差异可变。
- 2026-01-24 v1.21：统一 B.4 注意事项措辞风格与恢复确认触发条件表述。
- 2026-01-24 v1.20：补充协同效能边界句；强化 .geminiignore 恢复确认触发条件。
- 2026-01-24 v1.19：进一步精简项目平台差异表述；统一子项措辞。
- 2026-01-24 v1.18：强化中文沟通口径；收敛平台差异到项目耦合内容。
- 2026-01-21 v1.17：将批量改动说明格式指向 C.9 模板，补充恢复确认口径。
- 2026-01-21 v1.16：补充批量改动说明格式示例，完善 .geminiignore 恢复说明。
- 2026-01-21 v1.15：细化批量改动阈值与说明口径，约束 .geminiignore 临时调整。
- 2026-01-21 v1.14：调整平台差异边界，迁移操作/验证范式到共性基线。
- 2026-01-21 v1.13：优化变更影响模板展示，新增多文件/规则改写必填要求。
- 2026-01-21 v1.12：补充规则来源提示与模板示例，明确维护清单附录属性。
- 2026-01-21 v1.11：补充协作边界、变更影响模板与维护校验清单，完善验证豁免规则。
- 2026-01-21 v1.10：统一平台差异措辞，补充边界说明。
- 2026-01-21 v1.9：精简平台通用条款，保留项目内差异。
- 2026-01-20 v1.8：明确提交语言覆盖全局默认。
- 2026-01-20 v1.7：补充 Gemini 项目内操作范式。
- 2026-01-20 v1.6：强调与全局规则的协作边界与继承关系，减少重复表述。
- 2026-01-20 v1.5：强化项目规则的优先级与冲突处理表述。
- 2026-01-20 v1.4：明确项目级语言边界与协作分工，减少与全局重复。
- 2026-01-20 v1.3：去重通用约束，补齐 Gemini 项目内规则边界说明。
- 2026-01-20 v1.2：按三层结构重组规则，补齐 Gemini 项目差异说明。
- 2026-01-20 v1.1：统一项目模板，补齐 UI/数据/调试与技术债要点。

## 1. 阅读指引（项目级）
- 本文件为项目级规则，聚焦仓库特性与验证落地；全局共性与平台差异由 GlobalUser 对应文件提供。
- 三层结构：共性基线 + 平台差异 + 项目差异；各节自包含但避免重复。
- 规则冲突时以项目级为准，并在回复中说明采用原因。

## A. 共性基线（项目级）
- 稳定性优先：课堂场景禁止崩溃/卡死；外部交互失败必须降级。
- Interop 防御：所有 Win32/COM 调用必须 `try/catch`；错误不抛到 UI。
- 数据完整性：`students.xlsx`、`student_photos/`、`settings.ini` 结构不可破坏；写入保持原子替换（已有实现勿破坏）。
- 语言边界：对用户回复必须中文（除非用户明确要求英文）；面向教师/学生的 UI 文案尽量中文；代码/命令/日志等技术项保留英文；若平台默认英文或出现英文输出且用户未要求，需立即切回中文并继续。

### A.1 协作与优先级
- 继承：遵循全局共性基线与平台差异。
- 优先级：项目规则优先于全局规则；冲突需在回复中说明采用的规则。
- 边界：项目级仅补充仓库特定差异；为保证单文件可用，可做最小复述（每节建议≤3条，超出需说明原因）并指向全局文件，避免过度重复。
- 规则来源：对应平台的 GlobalUser 文件 → 项目根同名文件，以项目为准。
- 协同效能：全局提供稳定共性与平台差异，本项目聚焦仓库特性与验证落地，避免重复与缺失。

### A.2 操作与验证范式
- 定位：优先 `rg`/`rg --files` 定位，再打开必要文件；避免全量遍历 `student_photos/`。
- 批量改动：>=2 文件或跨模块视为批量，完成后复查受影响文件清单并说明范围。
- 说明格式：批量改动按 C.9 变更影响模板说明。
- 说明触发：最小复述条目超过 3 条或新增跨模块/跨平台约束时，需说明原因。
- 验证：多步骤优先 `powershell -File scripts/ctoolkit.ps1`，单点验证用 `dotnet test --filter ...`。
- 多副本目录：如存在多个 ClassroomToolkit 副本，执行前确认当前工作目录指向正确仓库根。

## B. 平台差异（Gemini CLI 项目内）
### B.1 加载与作用域
- 最小摘要：默认=`GEMINI.md`；可配 `context.fileName`/`contextFileName`；扫描=上下（`.geminiignore`/`.gitignore` 控制）。
- 操作：加载范围有疑问先对照 GlobalUser/GEMINI.md 的 B.1。
- 风险提示：扫描范围过大会引入噪声或遗漏，需结合 `.geminiignore` 控制。
### B.2 配置与上限
- 最小摘要：上限=200目录（`context.discoveryMaxDirs`/`memoryDiscoveryMaxDirs`，以文档为准）；记忆=`/memory show`/`refresh`/`add`。
- 操作：调整上限或刷新上下文前对照 GlobalUser/GEMINI.md 的 B.2。
- 风险提示：目录发现上限可能导致规则未加载，需结合诊断确认。
### B.3 诊断与命令
- 最小摘要：诊断=`/memory show`/`/memory refresh`。
- 操作：发现加载不全先运行上述命令，再对照 GlobalUser/GEMINI.md 的 B.3。
- 风险提示：诊断命令不改变加载结果，仅用于确认当前上下文。
### B.4 注意事项
- 注意事项：Gemini 可能向下扫描子树，必要时用 `.geminiignore` 排除 `student_photos/` 等非必要目录。
- 注意事项：项目根已提供 `.geminiignore`，默认用于排除非必要目录。
- 注意事项：仅在用户明确要求且确需读取媒体/照片时临时调整 `.geminiignore`，完成后恢复并在沟通中说明恢复状态。
- 注意事项：默认使用 `GEMINI.md` 作为上下文文件名；如需更名需同步 `settings.json` 并说明影响。
- 注意事项：上下文变更后优先执行 `/memory refresh` 并确认生效范围。
- 注意事项：本节仅写项目内 Gemini 细节，避免重复全局规则。

## C. 项目差异（领域/技术）
### 1. 目录与模块
- `src/ClassroomToolkit.App` — WPF UI (MVVM，含 MainViewModel、窗口工厂与启动 DI 入口)
- `src/ClassroomToolkit.Domain` — 纯业务逻辑
- `src/ClassroomToolkit.Services` — 应用服务编排
- `src/ClassroomToolkit.Interop` — Win32/COM 高风险区
- `src/ClassroomToolkit.App/Windowing` — 多窗口编排策略（Z-Order/Owner/Topmost）
- `src/ClassroomToolkit.Infra` — 配置/持久化
- `tests/ClassroomToolkit.Tests` — xUnit + FluentAssertions
- `scripts/ctoolkit.ps1` — 构建/测试脚本
- `docs/`、`tools/` — 支持材料
- `students.xlsx`、`student_photos/` — 示例数据

### 2. 构建/测试/运行
- `dotnet restore`
- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test --filter "FullyQualifiedName~PresentationControlServiceTests"`
- `dotnet test --filter "FullyQualifiedName~MainViewModelTests"`
- `dotnet test --filter "FullyQualifiedName~WindowOrchestratorTests"`
- `dotnet run --project src/ClassroomToolkit.App/ClassroomToolkit.App.csproj`
- `dotnet build ClassroomToolkit.sln -c Release`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Release`
- `powershell -File scripts/ctoolkit.ps1`（可加 `-SkipTests`/`-SkipCommit`）
### 3. 代码规范
- 4 spaces 缩进，使用 file-scoped namespaces
- 类型/方法 PascalCase，局部/参数 camelCase，接口 `I` 前缀
- Nullability 明确（`string?` vs `string`），不可变模型优先用 records
- 注释使用英文，强调“why”而非重复代码

### 4. UI 开发（WPF）
- 颜色/样式使用资源（如 `WidgetStyles.xaml` 或主题资源），避免硬编码
- 新窗口继承 `BaseWindow` 或严格遵循现有视觉样式

### 5. 变更边界与依赖
- 不改变 `students.xlsx` 结构或 `settings.ini` 格式
- 不提交真实学生数据；`students.xlsx` 与 `student_photos/` 仅为示例资产
- Interop 大幅重构需先沟通确认

### 6. 关键技术要点（高风险区）
- COM 对象需释放（`Marshal.ReleaseComObject`/`try/finally`）
- 处理 `RPC_E_CALL_REJECTED`（COM 忙）并降级
- P/Invoke 通常使用 `CharSet.Auto` 与 `SetLastError = true`
- WPS 幻灯片：手动 F5 时 `SlideShowWindows.Count` 可能为 0；`SlideShowSettings.Run()` 可跟踪页码；无法跟踪时降级为“会话级画布”
- WPS ProgIDs：优先 `KWPP.Application`，退化 `WPP.Application`

### 7. 调试与技术债（概要）
- 调试标签：`[WpsTracker]`、`[InkCache]`、`[PresentationControl]`、`[UIAutomation]`
- 技术债：F5 拦截（需 `RegisterHotKey`）、Ink 序列化性能、WPS COM 重试、High DPI 模糊
- DPI 说明：已启用 `app.manifest` + `PerMonitorV2`，跨屏场景需做人工验收（4K + 投影）

### 8. 验证与测试
- 框架：xUnit + FluentAssertions（coverlet 已启用）
- 轻量验证：按需跑单测或特定类（含 `MainViewModelTests`、`WindowOrchestratorTests`）；完整验证：`dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- Interop 难以单测，优先覆盖 Domain 逻辑
- 仅文档/规则/注释调整可不运行测试，但需说明未运行原因与风险。
- 若未运行测试，需在回复中说明原因与风险
- 发布前应执行 Release 验证：`dotnet build ClassroomToolkit.sln -c Release` 与 `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Release`
### 9. 变更影响模板
- 多文件/规则改写必须填写本模板，其他变更建议填写。
- 模板：影响模块=；影响数据/配置=；UI/交互=；Interop/外部依赖=；验证与回滚=。
- 示例：影响模块=App/UI；影响数据/配置=无；UI/交互=新增按钮；Interop/外部依赖=无；验证与回滚=未测/可回退提交。

### 10. 提交与PR
- Commit 用中文简短摘要，可含前缀如 `feat:`（覆盖全局默认英文）
- PR 需包含摘要、测试命令、UI 变更截图

## D. 维护校验清单（项目级）
说明：维护清单为附录性质，不属于三层结构。
- 同步：`AGENTS.md`、`CLAUDE.md`、`GEMINI.md` 三文件同步更新差异。
- 版本：版本号、最后更新、变更记录同步更新。
- 结构：保持 1/A/B/C/D 结构与编号一致。
- 边界：平台差异仅放平台代理细节，不复制共性或领域规则。
- 自包含：项目级文件在 B 节提供最小复述并指向 GlobalUser 文件。
- 一致性：项目级 A/C/D 内容三文件保持一致；仅 B 节允许平台差异。
- 验证：变更后补充验证命令或未执行原因与风险。


