# 依赖与还原基线

最后更新：2026-08-17

## 当前事实

- 目标框架：生产核心 `net10.0`，Interop/Services `net10.0-windows`，WPF App 与测试为 `net10.0-windows10.0.19041.0`，以编译期访问 Windows PDF API。
- `global.json` 禁止 prerelease，但未固定 feature band；本轮实际 SDK 为 `10.0.303`。
- 所有解决方案项目都有 `packages.lock.json`；CI 使用 `dotnet restore ClassroomToolkit.sln --locked-mode`。
- 本地开发可更新 lockfile，但依赖变化必须提交对应 lockfile，并运行 full profile。
- 标准发布包通过 `aka.ms/dotnet/10.0` 下载当前 x64 Desktop Runtime，使用补丁中性文件名并校验微软 Authenticode 签名；当前实测版本为 `10.0.11.50000`。

生产直接依赖按职责保留：

- App：Microsoft DI/Logging、System.Speech；PDF 渲染使用 Windows 随系统提供的 `Windows.Data.Pdf`，不再携带第三方 PDF NuGet/native runtime。
- Infra：ClosedXML、OpenXML/Fonts/Packaging 补丁版本固定、Microsoft.Data.Sqlite/SQLitePCL、Logging。
- Services：System.Speech。
- Domain、Application、Interop：无第三方包。

测试项目只直接引用测试框架、覆盖率和其源码真正使用的 ClosedXML；生产项目已提供的包不再在测试项目重复固定。未使用的 `SourceGear.sqlite3` native SQLite 实现已删除，SQLite 统一由 `Microsoft.Data.Sqlite + SQLitePCLRaw.bundle_e_sqlite3` 提供。

## 验证

```powershell
dotnet restore ClassroomToolkit.sln --locked-mode -m:1
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-dependency-vulnerabilities.ps1
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-dependency-upgrade-feasibility.ps1
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile full -Configuration Debug
```

跨 major 升级遵循 `scripts/quality/dependency-outdated-waivers.json` 的有效 waiver；不得为清空版本提示直接升级会改变字体度量、工作簿、PDF、WPF 或测试平台行为的依赖。

## 边界与回滚

- lockfile/漏洞/analyzer 通过只证明仓库依赖闭包；Windows PDF 系统 API、真实课件视觉与 Office/WPS 仍需课堂设备现场验收。
- 回滚依赖切片时，同时回滚 `.csproj` 与对应 `packages.lock.json`，再执行 locked restore 和 full profile。
