规则ID=R1,R2,R4,R6,R8
影响模块=src/ClassroomToolkit.App/Paint
当前落点=PaintOverlayWindow.Photo.Navigation.cs / PhotoInkCurrentPageClipPolicy.cs / PhotoInkPreviewClipPolicy.cs
目标归宿=PDF/图片全屏时，页面外区域也可生成并显示笔迹（不再被当前页矩形裁剪）
迁移批次=2026-03-31-batch-3
风险等级=中（影响 photo ink clip 与全屏书写行为）
执行命令=
1) dotnet build ClassroomToolkit.sln -c Debug
2) dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
3) dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
4) powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=
- build: PASS
- test: PASS（3027 passed）
- contract/invariant: PASS（24 passed）
- hotspot: PASS
- 新增/修改测试:
  - tests/ClassroomToolkit.Tests/PhotoInkCurrentPageClipPolicyTests.cs
  - tests/ClassroomToolkit.Tests/PhotoInkPreviewClipPolicyTests.cs
变更文件=
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.cs
- src/ClassroomToolkit.App/Paint/PhotoInkCurrentPageClipPolicy.cs
- src/ClassroomToolkit.App/Paint/PhotoInkPreviewClipPolicy.cs
- tests/ClassroomToolkit.Tests/PhotoInkCurrentPageClipPolicyTests.cs
- tests/ClassroomToolkit.Tests/PhotoInkPreviewClipPolicyTests.cs
回滚动作=
- git restore --source=HEAD -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.cs src/ClassroomToolkit.App/Paint/PhotoInkCurrentPageClipPolicy.cs src/ClassroomToolkit.App/Paint/PhotoInkPreviewClipPolicy.cs tests/ClassroomToolkit.Tests/PhotoInkCurrentPageClipPolicyTests.cs tests/ClassroomToolkit.Tests/PhotoInkPreviewClipPolicyTests.cs

[根因]
- 全屏 PDF/图片 + 跨页显示启用时，UpdatePhotoInkClip() 通过 PhotoInkCurrentPageClipPolicy/PhotoInkPreviewClipPolicy 将 RasterImage 与预览层裁剪到当前页面矩形。
- 当鼠标/手指位于页面外区域，笔迹即使被生成，也会被 clip 裁掉，表现为“写不出来”。

[修复策略]
1) 在两个 clip policy 增加参数 photoFullscreenActive。
2) 当 photoFullscreenActive=true 时直接返回 Rect.Empty（不施加裁剪）。
3) 调用点 UpdatePhotoInkClip() 传入 IsPhotoFullscreenActive。
4) 增加策略测试：全屏时 clip 必须为空。

[平台诊断与N/A]
- cmd: codex status
  exit_code: 1
  key_output: Error: stdin is not a terminal
  timestamp: 2026-03-31
  na_type: platform_na
  reason: 非交互终端下 codex status 需要 TTY，无法直接执行。
  alternative_verification: 已执行 codex --version（codex-cli 0.117.0）与 codex --help（命令列表可用）作为平台可用性替代证据。
  evidence_link: docs/change-evidence/20260331-fullscreen-photo-pdf-ink-outside-page.md
  expires_at: 2026-04-30
- cmd: codex --version
  exit_code: 0
  key_output: codex-cli 0.117.0
  timestamp: 2026-03-31
- cmd: codex --help
  exit_code: 0
  key_output: commands listed (exec/review/login/...)
  timestamp: 2026-03-31
