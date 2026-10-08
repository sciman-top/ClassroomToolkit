# ClassroomToolkit 当前架构

架构事实最后复核：2026-10-08

验证入口更新：2026-10-07

## 终态判断

本项目保留 Windows-first 的 `.NET 10 + WPF` 模块化单体。WPF 与现有 Win32/COM/WPS、触控、窗口层级和本地数据链路匹配；拆成微服务、跨平台 UI 或全量重写会放大课堂现场风险，没有当前证据支持。

依赖方向以项目引用和 `ArchitectureDependencyTests` 为准：

- `Domain`：纯规则与模型，不依赖外层。
- `Application`：用例与外部能力 interface，只依赖 `Domain`。
- `Infra`：配置、工作簿、SQLite、日志等 adapter，依赖 `Application/Domain`。`StudentWorkbookStore` 负责工作簿文件的原子写入、外部变更检测与备份；`StudentWorkbookSpreadsheetCodec` 负责 `students.xlsx` 的读取、规范化和写入格式。
- `Interop`：Win32/COM/WPS 等高风险外部接入，保持独立。
- `Services`：Presentation、Input、Speech 等运行时 adapter，依赖 `Application/Domain/Interop`。
- `App`：WPF 生命周期、Session 与 Windowing；`App.xaml.cs` 与 `Startup/AppCompositionRoot.cs` 共同构成组合根，只有组合根接入 Infra。`Windowing` 负责系统窗口 Interop，`Paint` 仅在 Presentation seam 显式消费 Presentation Interop；`PaintPresentationRuntimeFactory` 集中装配 Paint 的 Presentation 运行时。
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

## 当前可选存储路径

- `students.xlsx` 仍是名册读取成功时的权威来源。启用 SQLite 时，`StudentWorkbookSqliteStoreAdapter` 保留 SQLite 快照，以便工作簿 bridge 失败时恢复会话；不能只按“已有 SQLite 文件”判断名册迁移已完成。
- Ink 历史启用 SQLite adapter 时仍保留 sidecar bridge；读取会仲裁两侧版本，保存要求两侧都成功。关闭 SQLite 路由会回到 sidecar 路径，不会删除现有 SQLite 文件。
- `CTOOLKIT_USE_SQLITE_BUSINESS_STORE` 与 `CTOOLKIT_ENABLE_EXPERIMENTAL_SQLITE_BACKEND` 都必须启用且运行时 SQLite 可用，才选择 SQLite 路由；这两个进程级开关由 `Startup/StorageBackendFeatureFlags` 在组合时读取。
- 删除 SQLite adapter、开关或快照兼容前，须先完成数据权威迁移、旧数据库处置与回滚验证；日常模块整理不得承担这项迁移。

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
