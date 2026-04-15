# 2026-04-06 区域截图入白板 + 设置分层

- rule_id: `R1/R2/R6/R8`
- risk_level: `medium`
- scope:
  - 工具条新增 `区域截图入白板`
  - `PaintWindowOrchestrator -> MainWindow` 事件链接线
  - 新增区域选择与截图落盘工作流
  - 画笔设置新增“显示高级兼容与排障选项”，默认隐藏高级区块

## 执行命令

1. `dotnet build ClassroomToolkit.sln -c Debug`
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## 验证证据

- build: 通过（0 error）
- test: 通过（3171 passed）
- contract/invariant: 通过（25 passed）
- hotspot: PASS（全部热点文件在线数预算内）

## platform_na

- type: `platform_na`
- reason: `codex status` 在当前非交互终端返回 `stdin is not a terminal`
- alternative_verification:
  - `codex --version` => `codex-cli 0.118.0`
  - `codex --help` 正常返回命令帮助
- evidence_link: `docs/change-evidence/20260406-region-capture-and-settings-layering.md`
- expires_at: `2026-05-06`

## 回滚动作

1. 回滚以下文件到变更前版本：
   - `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml`
   - `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`
   - `src/ClassroomToolkit.App/Services/PaintWindowOrchestrator.cs`
   - `src/ClassroomToolkit.App/MainWindow.Paint.cs`
   - `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml`
   - `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml.cs`
   - `src/ClassroomToolkit.App/Paint/RegionSelectionOverlayWindow.xaml`
   - `src/ClassroomToolkit.App/Paint/RegionSelectionOverlayWindow.xaml.cs`
   - `src/ClassroomToolkit.App/Paint/RegionScreenCaptureWorkflow.cs`
   - `README.md` / `README.en.md` / `使用指南.md`
2. 重跑门禁链：`build -> test -> contract/invariant -> hotspot`。

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
