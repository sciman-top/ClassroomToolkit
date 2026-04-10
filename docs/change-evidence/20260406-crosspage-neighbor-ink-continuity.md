规则ID=R1/R2/R3/R6/R8
影响模块=src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.cs; tests/ClassroomToolkit.Tests/CrossPageNeighborInkRenderSurfaceContractTests.cs
当前落点=跨页邻页墨迹位图解析（ResolveNeighborInkBitmap）
目标归宿=跨页交互渲染在缓存未命中时仍保持上一页墨迹连续可见
迁移批次=20260406-2
风险等级=中
执行命令=
- dotnet build ClassroomToolkit.sln -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
- powershell -File scripts/quality/check-hotspot-line-budgets.ps1
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~CrossPageNeighborInkRenderSurfaceContractTests"
验证证据=
- 新增同步兜底：`allowDeferredRender=false` 且已有 strokes 时，立即 `BuildNeighborInkCacheEntry` 并回填 `_neighborInkCache`，避免返回 null 或仅旧帧。
- 新增契约断言：确保 `ResolveNeighborInkBitmap` 保留同步重建路径。
- 门禁结果：build/test/contract/hotspot 全通过。
回滚动作=
- git restore --source=HEAD~1 src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.cs tests/ClassroomToolkit.Tests/CrossPageNeighborInkRenderSurfaceContractTests.cs

# Backfill 2026-04-03
执行命令=backfill-evidence-template-fields.ps1
验证证据=template-field-backfill-2026-04-03
回滚动作=git revert evidence backfill commit
