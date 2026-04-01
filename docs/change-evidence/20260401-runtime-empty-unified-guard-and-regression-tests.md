规则ID=R1,R2,R3,R4,R6,R8
影响模块=src/ClassroomToolkit.App/Paint + tests/ClassroomToolkit.Tests
当前落点=统一 runtime-empty 守卫接入（current redraw + current fast-path + visible-neighbor prime）+ 回归契约测试扩展
目标归宿=clear-all(empty) 页面在任意入口都优先命中守卫，不再被后续拖拽/缩放/邻页预热链路回灌旧笔迹
迁移批次=2026-04-01-06
风险等级=中
执行命令=dotnet build ClassroomToolkit.sln -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests|FullyQualifiedName~PaintOverlayClearAllCrossPageRecoveryContractTests"; powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=build 0 error; 全量测试 3058 通过; contract/invariant+clearall-contract 29 通过; hotspot PASS
快速复验= powershell -File scripts/validation/run-stable-tests.ps1 -Configuration Debug -SkipBuild -Profile quick -> 56 通过, 0 失败
回滚动作=git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.cs tests/ClassroomToolkit.Tests/PaintOverlayClearAllCrossPageRecoveryContractTests.cs docs/change-evidence/20260401-runtime-empty-unified-guard-and-regression-tests.md
platform_na.reason=codex status 在非交互终端返回 "stdin is not a terminal"，无法提供可用状态输出
platform_na.alternative_verification=执行 codex --version(codex-cli 0.118.0) 与 codex --help(exit_code=0) 作为平台可用性替代证据
platform_na.evidence_link=docs/change-evidence/20260401-runtime-empty-unified-guard-and-regression-tests.md
platform_na.expires_at=2026-04-08
