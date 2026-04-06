# 2026-04-07 WPS 默认放映控制策略切换为 auto

- rule_id: `R1/R2/R6/R8`
- risk_level: `low`
- current_landing: `WPS 放映控制默认值与设置面板推荐文案`
- target_destination: `新装/默认配置下 WPS 默认使用 auto（raw 优先 + message 回退）`
- migration_batch: `2026-04-07-batch-1`

## 变更文件

- `src/ClassroomToolkit.App/Settings/AppSettings.cs`
  - `WpsInputMode` 默认值由 `message` 调整为 `auto`。
- `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml.cs`
  - WPS 策略下拉文案调整为“自动判断（推荐）”。
  - 对话框内部默认 `WpsInputMode` 调整为 `auto`。
- `tests/ClassroomToolkit.Tests/AppSettingsServiceTests.cs`
  - 同步默认值断言到 `auto`。
- `tests/ClassroomToolkit.Tests/PresetSchemePolicyTests.cs`
  - 修复与 `AppSettings` 新默认值解耦：在需匹配预设参数的测试中显式指定 `WpsInputMode=message`。

## 执行命令

1. `dotnet build ClassroomToolkit.sln -c Debug`
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## 验证证据

- build: 通过（0 error）
- test: 通过（3189 passed）
- contract/invariant: 通过（25 passed）
- hotspot: PASS

## 阻断与处理记录

- issue: 首次 `build` 失败（`sciman Classroom Toolkit.exe` 被运行中进程锁定）
- action: `Stop-Process -Id 35520 -Force` 后重跑
- result: 门禁链路全部通过

## 回滚动作

1. 回滚以下文件到变更前版本：
   - `src/ClassroomToolkit.App/Settings/AppSettings.cs`
   - `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml.cs`
   - `tests/ClassroomToolkit.Tests/AppSettingsServiceTests.cs`
   - `tests/ClassroomToolkit.Tests/PresetSchemePolicyTests.cs`
2. 重跑门禁链：`build -> test -> contract/invariant -> hotspot`
