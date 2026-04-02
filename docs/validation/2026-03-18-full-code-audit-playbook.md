# 全量代码审查执行手册（sciman课堂工具箱）

最后更新：2026-03-18  
适用范围：`src/` 与 `tests/` 全仓审查、PR 闸门、发布前回归

## 1. 目标与退出条件

- 目标不是“绝对零缺陷”，而是把漏错概率压到可接受范围，并形成可持续复用的审查流水线。
- 审查完成必须同时满足：
  - CI 质量闸门全绿（含 `Debug/Release` 构建与测试）。
  - 高风险专项（Interop/Windowing/跨页 Ink/数据落盘）无阻断缺陷。
  - 人工回归清单执行完毕且留档。

## 2. 风险分层与归宿

- `App/View/Windowing`：UI 线程、窗口焦点、置顶/激活、交互状态切换。
- `Application/Services`：用例编排、外部能力桥接、跨模块流程一致性。
- `Domain`：纯业务规则、状态机、边界值与不变量。
- `Infra`：配置读写、`students.xlsx` 兼容、SQLite 持久化与原子替换。
- `Interop`：Win32/COM/WPS Hook、异常拦截、重试与降级。

## 3. 执行流水线（必须按顺序）

### 3.1 阶段 A：静态闸门

```powershell
dotnet restore ClassroomToolkit.sln --locked-mode
dotnet list ClassroomToolkit.sln package --vulnerable --include-transitive
dotnet build ClassroomToolkit.sln -c Debug --no-restore /warnaserror
dotnet build ClassroomToolkit.sln -c Release --no-restore /warnaserror
```

判定：
- 任意漏洞报告、编译错误、告警升级错误，均视为未通过。

### 3.2 阶段 B：架构与契约闸门

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
```

判定：
- 架构依赖方向、Interop 生命周期契约、关键跨页生命周期契约全部通过。

### 3.3 阶段 C：全量自动化回归

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --collect:"XPlat Code Coverage" --results-directory artifacts/TestResults/Debug --logger "trx;LogFileName=debug.trx" --blame-hang --blame-hang-timeout 5m
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Release --no-build --results-directory artifacts/TestResults/Release --logger "trx;LogFileName=release.trx" --blame-hang --blame-hang-timeout 5m
```

判定：
- 全量测试通过。
- 无挂死（hang blame）报告。

### 3.4 阶段 D：高风险专项审查

重点文件（首轮）：
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.cs`
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.cs`
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.cs`
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs`
- `src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml.cs`
- `src/ClassroomToolkit.Interop/Presentation/KeyboardHook.cs`
- `src/ClassroomToolkit.Interop/Presentation/WpsSlideshowNavigationHook.cs`
- `src/ClassroomToolkit.Interop/Utilities/ComObjectManager.cs`
- `src/ClassroomToolkit.Infra/Storage/RollCallSqliteStoreAdapter.cs`
- `src/ClassroomToolkit.Infra/Storage/StudentWorkbookSqliteStoreAdapter.cs`

审查清单：
- 异常策略：是否仅拦截可恢复异常，是否保留必要错误语义。
- 资源生命周期：COM/Hook/订阅是否成对释放，失败路径是否清理。
- 并发与线程：UI 线程访问、`async void`、`Task.Run`、阻塞调用（如 `Thread.Sleep`）是否可控。
- 数据一致性：原子替换/降级路径是否会破坏 `settings.ini`、`students.xlsx` 兼容。
- 退化行为：WPS/Interop 失败是否可重试、可降级、可提示。

### 3.5 阶段 E：人工回归与验收

- 按 `docs/validation/manual-final-regression-checklist.md` 完成课堂场景验证。
- 重点执行：
  - PPT/WPS 全屏进入退出与翻页
  - 图片/PDF/白板互切
  - 多窗口置顶/焦点/输入一致性
  - 高 DPI 跨屏可读性

## 4. 缺陷闭环要求

- 每个缺陷必须有：复现步骤、根因、修复点、回归测试。
- 线上/人工发现缺陷，必须补至少一个自动化防回归测试。
- 任何紧急止血补丁，必须记录“最终归宿模块”，禁止长期堆叠在窗口热点文件。

## 5. 结果归档

- 建议证据目录：`docs/validation/evidence/YYYY-MM-DD-full-audit/`
- 至少归档：
  - `trx` 测试报告
  - 覆盖率原始文件（`coverage.cobertura.xml`）
  - 人工回归截图与关键日志

## 6. 回滚准则

- 任一阻断级问题（崩溃、卡死、输入失效、数据写坏）立即停止发布。
- 回滚依据：`docs/runbooks/migration-rollback-playbook.md`
- 回滚后重新执行：
  - 阶段 A（静态闸门）
  - 阶段 C（全量自动化回归）
  - 阶段 E（人工回归）

## 7. 变更影响模板（执行时必填）

`影响模块=；影响数据/配置=；UI/交互=；Interop/外部依赖=；验证与回滚=`

