# 2026-04-19 设置默认值与推荐值优化

- rule_id: `R1/R2/R3/R6/R8`
- risk_level: `medium`
- current_landing: `画笔预设默认值 / 点名设置默认值与推荐文案`
- target_destination: `新装默认、设置页推荐文案、预设匹配逻辑一致；点名设置保持保守默认但推荐更明确`

## Basis

- 画笔侧存在默认语义不一致：
  - `WpsInputMode` 默认值与设置文案已切到 `auto`，但 `Balanced/Responsive` 预设仍锁到 `message`，导致默认配置进入设置页时可能被识别为 `custom`。
  - `WpsDebounceDefaultMs` 仍保留为历史 `200ms`，而当前真实默认已是 `120ms`。
- 点名侧无需新增场景预设，但现有默认/推荐表达偏弱：
  - 显示页未明确“姓名优先、学号按需”。
  - 语音/遥控/中途提醒虽默认关闭，但文案没有显式说明默认建议。
  - 结束音/提醒音/遥控键缺少推荐标签。

## Changes

1. 画笔预设与默认语义对齐
- `PaintPresetDefaults`
  - 将 `WpsDebounceDefaultMs` 收口到当前默认 `120ms`。
  - 新增 `WpsDebounceLegacyDefaultMs=200` 保留历史兼容口径。
- `PresetSchemePolicy`
  - `Balanced` / `Responsive` 的托管 `WpsInputMode` 改为 `auto`。
  - `Stable` 保持 `message`，继续承担“高稳定/高兼容”语义。
- `PresetSchemeInitializationPolicy`
  - 首次推荐初始化允许当前默认 `auto` 与历史默认 `message` 两种 WPS 模式。
  - 去抖默认判断改为“当前默认 `120ms` + 历史默认 `200ms`”。

2. 点名默认值与推荐文案优化
- `AppSettings`
  - `RollCallShowId` 默认值改为 `false`，新装默认优先突出姓名，减少课堂视觉噪音。
- `RollCallSettingsDialog.xaml`
  - 显示页改为“显示姓名（推荐）/ 显示学号（按需）”。
  - 语音、遥控、中途提醒统一补充“按需”提示。
  - 结束音效补充“推荐”提示。
  - 页脚说明改为显式默认建议：
    - 显示：默认建议只显示姓名。
    - 语音：默认关闭，播报设备跟随系统。
    - 遥控：默认关闭，启用时优先 `Tab` 点名、`Enter` 切组。
    - 提醒：默认保留结束提示，中途提醒按需开启。
- `RollCallSettingsDialog.xaml.cs`
  - 遥控键选项补充推荐标签。
  - 结束音 / 提醒音默认项补充推荐标签。

3. 文档与测试同步
- 更新 `使用指南.md` 中点名结果描述，避免默认仍写成“总是显示学号”。
- 更新/新增默认值与预设匹配测试，确保：
  - 当前默认配置会被识别为 `Balanced`。
  - 首次推荐初始化会从 `auto` 正常切换到 `Stable` 推荐。
  - 点名默认值断言与 UI 文案契约同步。

## Commands / Evidence

1. `codex --version`
- exit_code: `0`
- key_output: `codex-cli 0.121.0`

2. `codex --help`
- exit_code: `0`
- key_output: `Codex CLI help`

3. `codex status`
- exit_code: `1`
- type: `platform_na`
- reason: `stdin is not a terminal`
- alternative_verification: `codex --version` + `codex --help`
- evidence_link: `docs/change-evidence/20260419-settings-defaults-optimization.md`
- expires_at: `2026-04-26T23:59:59+08:00`

4. `dotnet build ClassroomToolkit.sln -c Debug`
- exit_code: `0`
- key_output: `0 warnings, 0 errors`

5. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- first_exit_code: `1`
- first_key_output: `PresetSchemeInitializationPolicyTests.Resolve_ShouldOnlyMarkInitialized_WhenManagedValuesWereManuallyChanged` 旧假设未同步
- remediation: 将该测试的“手动改动”输入从 `auto` 调整为 `raw`
- final_exit_code: `0`
- final_key_output: `Passed 3304, Failed 0`

6. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- exit_code: `0`
- key_output: `Passed 28, Failed 0`

7. 辅助定向测试
- command: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~AppSettingsDefaultsTests|FullyQualifiedName~PaintPresetDefaultsTests|FullyQualifiedName~PresetSchemePolicyTests|FullyQualifiedName~PresetSchemeInitializationPolicyTests|FullyQualifiedName~UiCopyContractTests"`
- exit_code: `1`
- type: `platform_na`
- reason: `VBCSCompiler` 共享编译进程锁住 `ClassroomToolkit.Tests.dll`
- alternative_verification: 标准全量 `dotnet test` 已通过，且契约测试已通过
- evidence_link: `docs/change-evidence/20260419-settings-defaults-optimization.md`
- expires_at: `2026-04-26T23:59:59+08:00`

## Hotspot Review

- review_scope:
  - `src/ClassroomToolkit.App/Paint/PresetSchemePolicy.cs`
  - `src/ClassroomToolkit.App/Paint/PresetSchemeInitializationPolicy.cs`
  - `src/ClassroomToolkit.App/Paint/PaintPresetDefaults.cs`
  - `src/ClassroomToolkit.App/Settings/AppSettings.cs`
  - `src/ClassroomToolkit.App/RollCallSettingsDialog.xaml`
  - `src/ClassroomToolkit.App/RollCallSettingsDialog.xaml.cs`
- review_point_1: 默认配置是否仍会落到 `custom`
  - conclusion: 已修复；`AppSettings` 当前默认值可被 `ResolveInitialScheme` 正确识别为 `Balanced`。
- review_point_2: `Stable` 预设是否仍保留高兼容语义
  - conclusion: 保留；`Stable` 继续使用 `message`，仅 `Balanced/Responsive` 收口到 `auto`。
- review_point_3: 点名默认是否过于激进
  - conclusion: 未激进放宽；语音、遥控、中途提醒仍默认关闭，仅把“学号显示”改为默认关闭以减少课堂噪音。
- review_point_4: 外部契约/配置格式
  - conclusion: 未改配置键名、序列化格式与持久化结构，仅调整默认值与 UI 推荐文案。

## Rollback

1. 回滚以下文件：
- `src/ClassroomToolkit.App/Settings/AppSettings.cs`
- `src/ClassroomToolkit.App/Paint/PaintPresetDefaults.cs`
- `src/ClassroomToolkit.App/Paint/PresetSchemePolicy.cs`
- `src/ClassroomToolkit.App/Paint/PresetSchemeInitializationPolicy.cs`
- `src/ClassroomToolkit.App/RollCallSettingsDialog.xaml`
- `src/ClassroomToolkit.App/RollCallSettingsDialog.xaml.cs`
- `tests/ClassroomToolkit.Tests/AppSettingsDefaultsTests.cs`
- `tests/ClassroomToolkit.Tests/PaintPresetDefaultsTests.cs`
- `tests/ClassroomToolkit.Tests/PresetSchemePolicyTests.cs`
- `tests/ClassroomToolkit.Tests/PresetSchemeInitializationPolicyTests.cs`
- `tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs`
- `使用指南.md`

2. 重跑门禁链：
- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
