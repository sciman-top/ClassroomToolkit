# 2026-04-19 工具条橡皮擦/区域擦除选中态底边裁剪修复

- Rule IDs: R1, R2, R6, R8
- Risk Level: Low（仅布局参数调整，无行为改动）
- Scope: `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml`

## 依据
- 现象：`EraserButton`、`RegionEraseButton` 在选中高亮时底边出现裁剪，其他图标按钮正常。
- 根因：工具条外层容器有效内容高度为 `36`（固定高 `46`，减去外边框与 `Padding="8,4"`）。擦除工具组内按钮高 `32`，外包分组 `Border` 原值 `Padding="4,3"` 导致分组总高 `40`，超出后被父容器裁剪；选中态因为边框/高对比更显眼。

## 变更
- 将擦除工具组容器 `Padding` 从 `4,3` 调整为 `4,1`，使组高度收敛到 `36`，与外层可用高度一致。
- 保持图标资源、按钮事件、模式切换逻辑不变。

## 平台 N/A 留痕
- type: `platform_na`
- cmd: `codex status`
- reason: 非交互终端返回 `stdin is not a terminal`
- alternative_verification: 使用 `codex --version`、`codex --help` 完成平台最小诊断
- evidence_link: 本文“执行命令与关键输出”
- expires_at: `2026-05-19`

## 执行命令与关键输出
1. `codex --version`
   - exit_code: 0
   - key_output: `codex-cli 0.121.0`
2. `codex --help`
   - exit_code: 0
   - key_output: `Usage: codex [OPTIONS] [PROMPT]`
3. `codex status`
   - exit_code: 1
   - key_output: `Error: stdin is not a terminal`
4. `dotnet build ClassroomToolkit.sln -c Debug`
   - exit_code: 0
   - key_output: `0 个错误`
5. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
   - exit_code: 0
   - key_output: `通过: 3273, 失败: 0`
6. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - exit_code: 0
   - key_output: `通过: 28, 失败: 0`

## Hotspot 人工复核
- 文件：`src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml`
- 复核点：
  - 仅一处布局参数变更：擦除工具组 `Border.Padding`；
  - `EraserButton` / `RegionEraseButton` 的 Click 绑定、ToolTip、Foreground 与图标资源键均未变；
  - 无跨模块依赖与逻辑路径影响。
- 结论：风险收敛，可合入。

## 回滚
- 回滚文件：`src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml`
- 回滚动作：
  - 将擦除工具组 `Padding` 从 `4,1` 恢复为 `4,3`；
  - 依次重跑 `build -> test -> contract/invariant` 验证回滚一致性。
