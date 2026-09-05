# ClassroomToolkit 当前架构

最后更新：2026-08-17

## 终态判断

本项目保留 Windows-first 的 `.NET 10 + WPF` 模块化单体。WPF 与现有 Win32/COM/WPS、触控、窗口层级和本地数据链路匹配；拆成微服务、跨平台 UI 或全量重写会放大课堂现场风险，没有当前证据支持。

依赖方向以项目引用和 `ArchitectureDependencyTests` 为准：

- `Domain`：纯规则与模型，不依赖外层。
- `Application`：用例与外部能力 interface，只依赖 `Domain`。
- `Infra`：配置、工作簿、SQLite、日志等 adapter，依赖 `Application/Domain`。
- `Interop`：Win32/COM/WPS 等高风险外部接入，保持独立。
- `Services`：Presentation、Input、Speech 等运行时 adapter，依赖 `Application/Domain/Interop`。
- `App`：WPF、组合根、Session 与 Windowing；仅组合根接入 Infra，只有 `Windowing` 可直连 Interop。

## 模块与 seam

- 模块应在小 interface 后隐藏有价值的行为；调用者和测试都通过同一 seam 使用它。
- 生产 adapter 与测试 adapter 共同存在时，外部 seam 有真实价值；不得仅因“只有一个生产实现”删除 Interop 测试 seam。
- 单表达式 `*Policy`、仅转发参数的 wrapper、只被一个调用点和逐字复述测试使用的类型不是有效模块，应内联到所属行为。
- 新功能优先扩展已有高内聚模块。只有行为确实变化、需要替换或能集中多个调用者复杂性时，才增加 interface、adapter 或独立文件。
- 大型 WPF 窗口允许用 `partial` 文件按职责组织，但不能把业务规则、持久化或 Interop 细节继续散落到事件处理器。
- 功能增/删的固定触点见 [feature-template.md](feature-template.md)；新建决策类文件受碎片化门禁约束（`scripts/quality/check-hotspot-line-budgets.ps1`，≥15 行，例外仅限基线存量）。

## 演进顺序

1. 保持课堂主链、数据格式与 Interop 降级兼容。
2. 在触及具体路径时删除浅模块和重复源码形状测试。
3. 只有重复故障或多调用者复杂性证明收益时，才把逻辑深化到 Application、Session、Windowing 或 Infra seam。
4. 不做全量重写；每个切片用受影响测试与 build 收口，共享/高风险 seam 使用 standard，发布或依赖变化使用 full。

## 验证

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests"
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug
```

`repo_verified` 只证明仓库层依赖与自动化契约，不替代多显示器、DPI、投影、PPT/WPS 和触控设备的课堂现场验收。
