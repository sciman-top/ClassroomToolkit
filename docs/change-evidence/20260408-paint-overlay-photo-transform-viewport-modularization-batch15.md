规则ID=R1,R2,R5,R6,R8
影响模块=
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.cs
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.Viewport.cs
- tests/ClassroomToolkit.Tests/App/RegionCaptureWhiteboardIntegrationContractTests.cs
当前落点=PaintOverlayWindow.Photo.Transform.cs 含视口适配/居中缩放方法组，且契约测试对单文件位置有硬编码
目标归宿=将视口适配方法组迁移到独立 partial，并将契约测试改为 Transform partial 聚合断言，保持契约语义且允许模块化演进
迁移批次=2026-04-08-maintainability-hardening-batch15
风险等级=低
执行命令=
- dotnet build ClassroomToolkit.sln -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
- powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=
- build 通过：0 errors / 0 warnings
- 全量测试通过：3199 passed / 0 failed
- contract/invariant 通过：25 passed / 0 failed
- hotspot 通过：status=PASS
- 热点体量下降：PaintOverlayWindow.Photo.Transform.cs 从 1041 降到 860（预算 1100，delta 从 -59 改善到 -240）
回滚动作=
- git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.cs
- git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.Viewport.cs
- git checkout -- tests/ClassroomToolkit.Tests/App/RegionCaptureWhiteboardIntegrationContractTests.cs

# Backfill 2026-04-03
影响模块=legacy-governance-evidence
执行命令=backfill-evidence-template-fields.ps1
验证证据=template-field-backfill-2026-04-03
回滚动作=git revert evidence backfill commit
