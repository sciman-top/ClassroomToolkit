# 变更证据：区域截图穿透工具条后的模式切换修复

- date: 2026-04-06
- scope: `PaintToolbarWindow` 非白板交互行为修复 + 契约测试补充
- risk_level: 中

## 依据
- 问题现象：区域截图时进入工具条会取消截图态；用户点击工具条按钮后移出工具条仍会恢复十字截图态，且白板态到工具态切换不稳定。
- 目标：点击非白板按钮后不再自动恢复截图；并确保工具模式切换可退出白板态。

## 变更
- `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`
  - 非白板交互入口统一调用 `PrepareForNonBoardToolbarAction(...)`。
  - 在该入口内先 `ClearDirectWhiteboardEntryArm()`，避免离开工具条后自动恢复区域截图。
  - 对工具模式相关交互增加 `ExitWhiteboardForToolSwitchIfNeeded()`，确保白板态切换到对应工具模式时先退白板。
- `tests/ClassroomToolkit.Tests/App/RegionCaptureWhiteboardIntegrationContractTests.cs`
  - 新增契约测试 `ToolbarNonBoardActions_ShouldClearRegionCaptureResumeArm_AndExitWhiteboardForToolSwitch`。

## 执行命令与结果
- `codex status` -> 失败（`stdin is not a terminal`）
- `codex --version` -> 成功
- `codex --help` -> 成功
- `dotnet build ClassroomToolkit.sln -c Debug` -> 失败（二进制文件被运行中的 `sciman Classroom Toolkit (27248)` / `Microsoft Visual Studio (25556)` 锁定）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` -> 失败（同上锁定导致 `ClassroomToolkit.App` 项目复制失败）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"` -> 失败（同上锁定导致 `ClassroomToolkit.App` 项目复制失败）
- `powershell -File scripts/quality/check-hotspot-line-budgets.ps1` -> 成功（`status=PASS`）
- 复验（进程释放后）：
  - `dotnet build ClassroomToolkit.sln -c Debug` -> 成功（0 warning / 0 error）
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` -> 成功（3180 passed）
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"` -> 成功（25 passed）
  - `powershell -File scripts/quality/check-hotspot-line-budgets.ps1` -> 成功（`status=PASS`）

## N/A 记录
- type: `platform_na`
  - reason: 非交互终端下 `codex status` 不可用（`stdin is not a terminal`）。
  - alternative_verification: 使用 `codex --version` 与 `codex --help` 验证 CLI 可用性与命令集。
  - evidence_link: 本文件“执行命令与结果”。
  - expires_at: 2026-04-13

- type: `gate_na`
  - reason: 硬门禁 `build/test/contract` 在当前会话被外部进程锁文件阻断，导致无法完成完整链路。
  - alternative_verification: 已通过 `hotspot`；完成代码差异审阅 + 契约测试已补充（待解锁后执行门禁命令完成最终验证）。
  - evidence_link: 本文件“执行命令与结果”。
  - expires_at: 2026-04-07（已于 2026-04-06 复验通过，可视为已恢复）

## 回滚
- 回滚命令：
  - `git checkout -- src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`
  - `git checkout -- tests/ClassroomToolkit.Tests/App/RegionCaptureWhiteboardIntegrationContractTests.cs`
  - `git checkout -- docs/change-evidence/20260406-region-capture-toolbar-mode-switch.md`

# Backfill 2026-04-03
规则ID=BACKFILL-LEGACY-EVIDENCE-2026-04-03
影响模块=legacy-governance-evidence
当前落点=D:/OneDrive/CODE/ClassroomToolkit/docs/change-evidence
目标归宿=D:/OneDrive/CODE/repo-governance-hub/source/project/ClassroomToolkit/*
迁移批次=2026-04-03-evidence-backfill
风险等级=Low(documentation backfill only)
执行命令=backfill-evidence-template-fields.ps1
验证证据=template-field-backfill-2026-04-03
回滚动作=git revert evidence backfill commit
