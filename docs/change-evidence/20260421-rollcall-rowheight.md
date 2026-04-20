# 2026-04-21 RollCallWindow RowDefinition.Height 解析异常修复

- issue_id: `rollcallwindow-rowheight-xamlparse`
- risk_level: `low`
- scope: `src/ClassroomToolkit.App/RollCallWindow.xaml`

## 依据

- 运行时异常：
  - `System.Windows.Markup.XamlParseException`
  - `设置属性 System.Windows.Controls.RowDefinition.Height 时引发了异常`
  - 内部异常：`ArgumentException: "48" 不是属性 "Height" 的有效值`
- 定位行：`RollCallWindow.xaml` 第 `30` 行。
- 资源定义：`Size_Shell_TitleBar_Management` 为 `sys:Double`，不适用于 `RowDefinition.Height`（需要 `GridLength`）。

## 变更

- 文件：`src/ClassroomToolkit.App/RollCallWindow.xaml`
- 修改：
  - `RowDefinition Height="{StaticResource Size_Shell_TitleBar_Management}"`
  - -> `RowDefinition Height="48"`

## 执行命令与关键输出

1. 诊断
   - `codex --version` -> `codex-cli 0.121.0`
   - `codex --help` -> 正常输出帮助
   - `codex status` -> 非交互环境失败：`stdin is not a terminal`
   - N/A: `platform_na`
     - reason: 非交互终端导致状态命令不可用
     - alternative_verification: 使用 `codex --version` 与 `codex --help` 补证
     - evidence_link: 本文件
     - expires_at: `2026-05-21`

2. 构建门禁（build）
   - `dotnet build ClassroomToolkit.sln -c Debug`
   - 结果：`0 warning, 0 error`

3. 测试门禁（test）
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
   - 结果：`Passed 3383, Failed 0`

4. 契约门禁（contract/invariant）
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - 结果：`Passed 28, Failed 0`

5. hotspot 复核
   - 核查本次改动行为：仅修正类型不匹配，不改变标题栏视觉高度（仍为 `48`），无交互路径变更。

## 回滚

- `git checkout -- src/ClassroomToolkit.App/RollCallWindow.xaml`
- 若仅回滚本次提交，使用对应 commit 的 `git revert <commit_sha>`。
