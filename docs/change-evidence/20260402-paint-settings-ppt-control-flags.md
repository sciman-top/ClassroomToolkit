# 变更证据：Paint 设置对话框不再强制开启 PPT/WPS 控制

- 日期：2026-04-02
- 风险等级：中
- 规则映射：R1/R2/R6/R8

## 依据
- 用户反馈需梳理“运行期缓存 vs 持久化”，并检查设置联动错误。
- 发现 `PaintSettingsDialog.OnConfirm` 将 `ControlMsPpt/ControlWpsPpt` 强制写为 `true`，导致用户原有关闭状态在“仅确认设置”后被覆盖。

## 变更
- 文件：`src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml.cs`
  - 构造函数初始化：
    - `ControlMsPpt = settings.ControlMsPpt;`
    - `ControlWpsPpt = settings.ControlWpsPpt;`
  - `OnConfirm` 删除强制赋值 `true` 的行为，改为保留现有值。
- 文件：`tests/ClassroomToolkit.Tests/PaintSettingsDialogPresentationControlContractTests.cs`
  - 新增契约测试：防止再次出现“确认设置即强制开启控制”的回归。

## 执行命令
- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## 验证证据
- Build：通过（0 errors / 0 warnings）
- Test：通过（3062 passed）
- Contract/Invariant 子集：通过（24 passed）
- Hotspot：PASS

## 平台 N/A 记录
- 类型：`platform_na`
- 命令：`codex status`
- 原因：非交互终端返回 `stdin is not a terminal`
- 替代验证：执行 `codex --version`、`codex --help` 均成功
- 证据链接：本文档
- expires_at：2026-04-09

## 回滚动作
1. 还原 `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml.cs` 中对 `ControlMsPpt/ControlWpsPpt` 的赋值逻辑。
2. 删除 `tests/ClassroomToolkit.Tests/PaintSettingsDialogPresentationControlContractTests.cs`。
3. 重新执行同一套门禁命令确认回滚结果。

# Backfill 2026-04-03
当前落点=BACKFILL-2026-04-03
风险等级=BACKFILL-2026-04-03
规则ID=BACKFILL-2026-04-03
回滚动作=BACKFILL-2026-04-03
目标归宿=BACKFILL-2026-04-03
迁移批次=BACKFILL-2026-04-03
验证证据=BACKFILL-2026-04-03
影响模块=BACKFILL-2026-04-03
执行命令=BACKFILL-2026-04-03
