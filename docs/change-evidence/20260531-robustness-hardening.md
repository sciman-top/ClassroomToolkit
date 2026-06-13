# 2026-05-31 鲁棒性加固证据

## 范围
- 当前落点：`ClassroomToolkit` WPF 画笔设置、快捷画笔弹窗、WPS hook、区域截图蒙层。
- 目标归宿：修复审查中发现的低风险健壮性缺口，不改变 `settings.ini` 既有语义和课堂主流程。

## 规则与风险
- 规则：R2 小步闭环、R6 硬门禁、R7 兼容保护、R8 可追溯、E5 供应链。
- 风险等级：低到中。涉及 UI 运行时边界与 Interop hook 状态，但不新增外部依赖、不改变持久化字段名。

## 变更摘要
- 非有限画笔尺寸：`NaN` / `Infinity` 输入回退到安全默认值，避免绕过 `Math.Clamp` 后污染画笔宽度。
- WPS hook：停用时清理 suppressed keyboard keys，避免 Enter 等保留键在下一次启用时残留。
- 区域截图蒙层：工具条外部取消入口与内部穿透逻辑一致，正在框选时不取消选择。

## 验证命令
- `dotnet test tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~AppSettingsServiceTests.Load_ShouldFallbackQuickBrushSizePresets_WhenValuesAreNonFinite|FullyQualifiedName~WpsHookOrchestratorTests.ApplyDisabled_ShouldClearSuppressedKeyboardKeys|FullyQualifiedName~RegionCaptureWhiteboardIntegrationContractTests.ToolbarNonBoardPress_ShouldCancelActiveRegionSelection_WhenToolbarIsAboveMask"`：先红后绿，最终 3/3 pass。
- `dotnet build ClassroomToolkit.sln -c Debug`：0 warning / 0 error。
- `dotnet test tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj -c Debug`：3510/3510 pass。
- `dotnet test tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`：29/29 pass。
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality\check-hotspot-line-budgets.ps1`：PASS。
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality\check-dependency-vulnerabilities.ps1`：PASS，无漏洞包。
- `git diff --check`：PASS。

## N/A / 替代证据
- `platform_na`：`codex status` 在非 TTY 环境失败，错误为 `TERM is set to "dumb"`；替代证据为 `codex --version` 与 `codex --help` 已可用。
- `expires_at`：下次需要验证 Codex 加载链或平台状态时，在交互终端重跑 `codex status`。

## 回滚
- 回滚本次工作树改动即可恢复：相关文件集中在画笔设置、快捷颜色弹窗、截图蒙层、WPS hook 生命周期和本证据文件。
