规则ID=R1,R2,R4,R6,R8
影响模块=src/ClassroomToolkit.App/Paint
当前落点=PaintOverlayWindow.Ink.cs / PaintOverlayWindow.xaml.cs（三角形草稿中断策略）
目标归宿=未完成三角形仅为预览态；模式切换、形状切换、窗口失焦均取消草稿且不提交
迁移批次=2026-03-31-batch-2
风险等级=中（影响 Shape/Triangle 交互路径）
执行命令=
1) dotnet build ClassroomToolkit.sln -c Debug
2) dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
3) dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
4) powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=
- build: PASS
- test: PASS（3025 passed）
- contract/invariant: PASS（24 passed）
- hotspot: PASS
- 变更文件:
  - src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.cs
  - src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs
回滚动作=
- git restore --source=HEAD -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs

[增量修复-5]
问题描述=三角形首次点击会把第一条边退化为点，导致第二次拖动/点击后仅形成直线。
根因=第一次 pointer up 直接提交“第一边”，在点击无位移场景下 point1==point2。
修复策略=
1) 引入 _triangleAnchorSet（第一点锚定状态）。
2) 第一次点击仅设置锚点，不提交第一边。
3) 仅当第一点到 pointer up 距离超过阈值（2.5 DIP）时，提交第一边。
4) 未完成草稿判定纳入 _triangleAnchorSet，确保中断取消逻辑完整覆盖。
变更文件=
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.cs

[增量复验]
- build: PASS
- test: PASS（3025 passed）
- contract/invariant: PASS（24 passed）
- hotspot: PASS

[平台诊断与N/A]
- cmd: codex status
  exit_code: 1
  key_output: Error: stdin is not a terminal
  timestamp: 2026-03-31
  na_type: platform_na
  reason: 非交互终端下 codex status 需要 TTY，无法直接执行。
  alternative_verification: 已执行 codex --version（codex-cli 0.117.0）与 codex --help（命令列表可用）作为平台可用性替代证据。
  evidence_link: docs/change-evidence/20260331-triangle-draft-interrupt-policy.md
  expires_at: 2026-04-30
- cmd: codex --version
  exit_code: 0
  key_output: codex-cli 0.117.0
  timestamp: 2026-03-31
- cmd: codex --help
  exit_code: 0
  key_output: commands listed (exec/review/login/...)
  timestamp: 2026-03-31

# Backfill 2026-04-03
回滚动作=BACKFILL-2026-04-03
验证证据=BACKFILL-2026-04-03
执行命令=BACKFILL-2026-04-03
