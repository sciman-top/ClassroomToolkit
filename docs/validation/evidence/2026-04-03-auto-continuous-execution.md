# 自动连续执行报告（2026-04-03）

执行模式：`自动连续执行`  
执行范围：质量门 + 可自动化回归项  
关联矩阵：`docs/validation/manual-fullscreen-switch-regression-matrix-20260403.md`

## 1. 自动门禁链路

命令：

`powershell -File scripts/quality/run-local-quality-gates.ps1 -Profile full -Configuration Debug`

结果：

- build：PASS
- test（全量）：PASS（3123）
- contract-invariant：PASS（25）
- hotspot：PASS
- waiver-health：PASS
- evidence-completeness：PASS（100%）
- stable-tests（profile=full）：PASS（3123）

## 2. 自动化场景映射结果

| 场景ID | 可自动化程度 | 执行状态 | 证据 |
|---|---|---|---|
| S01/S02（PPT/WPS 全屏状态机策略） | 高（策略+契约） | PASS | `Presentation*PolicyTests`、`contract-invariant` |
| S03/S04（图片/PDF 全屏与导航） | 高（策略+契约） | PASS | `Photo*Tests`、`CrossPage*Tests` |
| S05/S06（白板与三者互切状态机） | 中高（状态机+契约） | PASS | `UiSession*Tests`、`BoardTransition*Tests` |
| S07（光标/绘图输入模式） | 高（策略） | PASS | `OverlayInput*`、`OverlayFocus*` |
| S08/S09（辅助窗口键盘路由） | 高（单测） | PASS | `AuxWindowKeyRoutingHandlerTests` |
| S10（辅助窗口滚轮路由） | 高（新增单测） | PASS | `AuxWindowWheelRoutingHandlerTests` |
| S11（置顶编排策略） | 高（策略） | PASS | `Floating*PolicyTests`、`Launcher*PolicyTests` |
| S12（焦点恢复策略） | 中高（策略） | PASS | `PresentationFocus*Tests` |
| S13（笔迹清空/保存/显示策略） | 高（策略+契约） | PASS | `Ink*Tests`、`PaintOverlay*ContractTests` |

## 3. 自动执行中处理的阻断

- 阻断：`evidence-completeness` 初次失败（证据文档字段缺失）。
- 处理：补齐 `docs/change-evidence/20260403-fullscreen-switch-focus-routing-fixes.md` 与 `docs/change-evidence/20260403-ink-photo-perf-smoothness.md` 的模板关键字段。
- 复跑结果：质量门全链通过。

## 4. 仍需人工执行项

以下必须人工环境确认（无法由单元测试替代）：

- 真实 Office/WPS 多版本 + 多显示器 DPI 下的全屏窗口识别稳定性。
- 工具条/点名/启动器在真实投影课堂中的“持续可见 + 持续置顶”体感。
- 30 分钟以上连续互切压测下的焦点恢复与输入钩子稳定性。


## 5. 追加连续压测（quick -> full -> quick）

执行时间：2026-04-03 21:13:35 +08:00

命令序列：

1. powershell -File scripts/quality/run-local-quality-gates.ps1 -Profile quick -Configuration Debug
2. powershell -File scripts/quality/run-local-quality-gates.ps1 -Profile full -Configuration Debug
3. powershell -File scripts/quality/run-local-quality-gates.ps1 -Profile quick -Configuration Debug

结果摘要：

- Round 1（quick）：PASS（contract 25 / stable-tests 56）
- Round 2（full）：PASS（test 3123 / contract 25 / stable-tests 3123）
- Round 3（quick）：PASS（contract 25 / stable-tests 56）
- hotspot/waiver-health/evidence-completeness：三轮均 PASS

备注：

- 首次执行误用 scripts/validation/run-local-quality-gates.ps1 路径，已纠正为 scripts/quality/run-local-quality-gates.ps1 后继续执行。

## 6. 追加修复（白板/照片恢复语义）

执行时间：2026-04-03 21:34:11 +08:00

改动要点：

- `PaintWindowOrchestrator.OnToolbarWhiteboardToggled(true)` 移除 `OverlayWindow.ExitPhotoMode()`，避免白板进入时强制摧毁照片场景。
- 新增契约测试：`PaintWindowOrchestratorWhiteboardContractTests`，锁定“白板开启不强退照片”。

验证命令（按硬门禁顺序）：

1. `dotnet build ClassroomToolkit.sln -c Debug`
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

结果：

- build：PASS
- test（全量）：PASS（3138）
- contract-invariant：PASS（25）
- hotspot：PASS

平台诊断（AGENTS B.2）：

- `codex status`：`platform_na`（`stdin is not a terminal`）
- `codex --version`：PASS（`codex-cli 0.118.0`）
- `codex --help`：PASS

## 7. 追加修复（缓存标记清理 + quality-gate profile 透传）

执行时间：2026-04-03 21:34:11 +08:00 后续轮次

改动要点：

- `PaintOverlayWindow.ExitPhotoMode()` 增加 `_boardSuspendedPhotoCache = false`，清理白板挂起照片缓存残留标记。
- 新增契约测试：`PaintOverlayWhiteboardPhotoCacheContractTests`。
- 修复 `scripts/quality/run-local-quality-gates.ps1`：`stable-tests` 步骤改为 `-Profile $Profile`，消除 profile 透传契约失败。

验证结果：

- build：PASS
- test（全量）：PASS（3139）
- contract-invariant：PASS（25）
- hotspot：PASS
- 过程记录：并行执行时出现一次 `MarkupCompile.cache` 文件锁，改为顺序重跑后通过。
