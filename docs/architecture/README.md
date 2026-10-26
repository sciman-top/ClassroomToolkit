# ClassroomToolkit 当前架构

架构事实最后复核：2026-10-25

验证入口更新：2026-10-07

## 终态判断

本项目保留 Windows-first 的 `.NET 10 + WPF` 模块化单体。WPF 与现有 Win32/COM/WPS、触控、窗口层级和本地数据链路匹配；拆成微服务、跨平台 UI 或全量重写会放大课堂现场风险，没有当前证据支持。

依赖方向以项目引用和 `ArchitectureDependencyTests` 为准：

- `Domain`：纯规则与模型，不依赖外层；不承担文件系统实现。
- `Application`：用例与外部能力 interface，只依赖 `Domain`。
- `FileSystem`：无上层项目依赖的通用原子文件写入能力，由 Infra 与 App 的 Ink 存储共用。
- `Infra`：配置、工作簿、日志等 adapter，依赖 `Application/Domain/FileSystem`。`StudentWorkbookStore` 负责工作簿文件的原子写入、外部变更检测与备份；`StudentWorkbookSpreadsheetCodec` 负责 `students.xlsx` 的读取、规范化和写入格式。
- `Interop`：Win32/COM/WPS 等高风险外部接入，保持独立。
- `Services`：Presentation、Input、Speech 等运行时 adapter，依赖 `Domain/Interop`。
- `App`：WPF 生命周期、Session 与 Windowing；`App.xaml.cs` 与 `Startup/AppCompositionRoot.cs` 共同构成组合根，Infra 只在组合根接入；App 的 Ink sidecar 通过低层 FileSystem 共享原子写入，不依赖 Infra 实现。`Windowing` 负责系统窗口 Interop，`Paint` 仅在 Presentation seam 显式消费 Presentation Interop；`PaintPresentationRuntimeFactory` 集中装配 Paint 的 Presentation 运行时。
- `App/Settings`：`AppSettingsService` 将类型化设置映射到 `ISettingsDocumentStore`；INI/JSON 文件格式、迁移、写入保护由 Infra adapters 承担。存储后端的环境开关属于 `Startup` 组合配置，不属于用户设置。
- `Paint`：窗口层负责 WPF 调度和显示；`PdfDocumentSession` 独占 PDF 文档、串行化页面访问并管理有界页面缓存。

## 模块与 seam

- 模块应在小 interface 后隐藏有价值的行为；调用者和测试都通过同一 seam 使用它。
- 生产 adapter 与测试 adapter 共同存在时，外部 seam 有真实价值；不得仅因“只有一个生产实现”删除 Interop 测试 seam。
- 单表达式 `*Policy`、仅转发参数的 wrapper、只被一个调用点和逐字复述测试使用的类型不是有效模块，应内联到所属行为。
- 新功能优先扩展已有高内聚模块。只有行为确实变化、需要替换或能集中多个调用者复杂性时，才增加 interface、adapter 或独立文件。
- 组合根按功能注册组维护；窗口生命周期只调用组合根入口，不复制 adapter 选择、设置迁移或 DI 注册细节。
- 窗口内部的高风险运行时也要有单一构造入口：`PaintOverlayWindow` 消费 `PaintPresentationRuntimeFactory` 的装配结果，行为仍由既有 Service/Policy 承担。
- Interop 依赖必须使用文件级显式 using；禁止用 `global using` 把高风险类型隐式传播到整个 App 编译单元。
- 大型 WPF 窗口允许用 `partial` 文件按职责组织，但不能把业务规则、持久化或 Interop 细节继续散落到事件处理器。

## 功能维护入口

`Startup/AppCompositionRoot.CreateServices` 声明注册，`Build` 创建容器并接通诊断日志；注册阶段不加载用户设置或创建窗口。`AppCompositionRootTests` 使用 DI 校验已注册实现类型的构造器依赖，不激活这些运行时服务；工厂委托内部依赖仍需对应行为测试验证。

| 功能 | 组合根注册入口 | 行为与窗口入口 | 共享依赖 |
| --- | --- | --- | --- |
| 点名 | `AddRollCallFeature` | `RollCallWindowFactory`、`RollCallViewModel`、`RollCallWorkbookUseCase` | Settings、Input/Speech、Domain 规则 |
| 画笔与放映 | `AddPaintFeature` | `IPaintWindowOrchestrator`、`PaintWindowFactory`、`PaintPresentationRuntimeFactory` | Ink、Settings、Session、Windowing |
| 图片管理 | `AddPhotoFeature` | `ImageManagerWindowFactory`、`ImageManagerWindowCoordinator`、`MainWindow.Photo.ImageManager.cs` | Ink、Settings、画笔照片模式、Windowing |
| 共享墨迹能力 | `AddSharedInkServices` | `InkPersistenceService`、`InkExportService` | sidecar |
| 设置、主题、日志、启动器 | `AddConfigurationAndSettings`、`AddApplicationSettings`、`AddLogging`、`AddShell` | `App`、`StartupOrchestrator`、`MainWindow` | 配置适配器与窗口协调 |

新增或移除功能时，先定位对应注册组，再核对 shell 命令、事件订阅/解绑、退出顺序和持久化兼容。注册组提供依赖的集中入口；当前各功能仍静态编译在 WPF App 中，删除注册组本身不能完成整项功能移除。图片管理与画笔共用墨迹能力，删除一侧时必须保留另一侧需要的共享服务。

## 本轮审查与剩余边界

2026-10-25 检查了项目引用、组合根、窗口工厂、MainWindow 功能接线、Session 状态协调、持久化合同及既有架构门禁。当前结论是保留模块化单体，在真实调用边界逐步收敛职责。

- 已收敛：点名的 workbook store、use case、hook/speech 和窗口工厂统一归入功能注册组；画笔和图片管理各有注册入口；两者共用的 Ink 单独注册。注册类型、singleton 生命周期、存储选择与懒加载行为保持一致。
- 已简化：删除 `MainWindowLoadedToggleActionPolicy`、`MainWindowToggleStatePolicy` 两个纯布尔透传类型，调用方直接分支/赋值。测试保护 ViewModel 可观察的按钮文案与属性通知。另删除 `MainWindowVisibleChangedPolicy`、`MainWindowOverlayInteractionStatePolicy` 和 `MainWindowZOrderDedupIntervalPolicy` 三个包装层；可见性直接判断，交互状态直接构造，去重间隔直接使用共享 Windowing Policy。
- 已收敛：原子文件写入已从 Domain 移入无上层依赖的 FileSystem 项目，Infra 的工作簿/设置 adapter 与 App 的 Ink sidecar/WAL/export 共同调用。写入算法、重试、flush 与临时文件清理行为保持原样，并由原有文件级测试覆盖；架构契约验证 Domain 不再依赖文件系统项目。
- 已收敛测试契约：Interop 只保留 Unicode marshalling 的 ABI 契约；Ink 不可访问枚举改为真实 ACL 降级行为测试；Release 契约保留清洁源码、数据隔离、完整性、锁定还原、版本戳和 channel 隔离，不再逐字断言实现措辞。
- 功能边界：图片管理生命周期由 `Photos/ImageManagerWindowCoordinator` 所有。MainWindow 通过回调衔接画笔照片模式、键盘导航和 Windowing/z-order；这些是当前功能间真实交互点，行为归属分别留在 Photos、Paint 和通用 Windowing 模块。
- 验收边界：架构契约与 DI 构造图校验覆盖依赖约束；不能证明反射/工厂内部装配、真实窗口显示、触控、COM 或课堂设备行为。课堂现场路径仍需对应环境验收。

## 存储路径

- `students.xlsx` 是名册读取成功时的权威来源，Ink 历史走 sidecar JSON；两者是仅有的持久化路径。
- SQLite 业务存储实验栈已于 2026-10-26 整体退役（ADR-004 裁决，证据见 `docs/change-evidence/20261026-sqlite-experimental-stack-retirement.md`）；历史遗留 `.sqlite3` 文件为惰性孤儿，无代码读写。

## 演进顺序

1. 保持课堂主链、数据格式与 Interop 降级兼容。
2. 在触及具体路径时删除浅模块和重复源码形状测试。
3. 只有重复故障或多调用者复杂性证明收益时，才把逻辑深化到 Application、Session、Windowing 或 Infra seam。
4. 不做全量重写；每个切片用受影响测试与 build 收口，共享/高风险 seam 使用 standard，发布或依赖变化使用 full。

## 验证

### 定向反馈

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests"
```

### 阶段收口

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug
```

标准门禁已包含 `ArchitectureDependencyTests`。若已运行标准门禁，不要再补跑这条定向测试；小切片可先用定向反馈，阶段收口时再按需选择标准门禁。

`repo_verified` 只证明仓库层依赖与自动化契约，不替代多显示器、DPI、投影、PPT/WPS 和触控设备的课堂现场验收。
