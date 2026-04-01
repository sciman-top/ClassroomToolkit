规则ID=R1,R2,R3,R4,R6,R8
影响模块=src/ClassroomToolkit.App/Paint
当前落点=PaintOverlayWindow.Export.cs + InkDirtyPageCoordinator.cs + PaintOverlayWindow.Photo.CrossPage.cs + PaintOverlayWindow.xaml.cs（回填准入/运行时状态键/邻页旧帧回退/最小诊断点）
目标归宿=清空后运行时状态优先；sidecar/sqlite持久化单一路径；跨页刷新不再回灌冲突旧快照；相对/绝对路径状态一致；按开关输出可定位日志
迁移批次=2026-04-01-01
风险等级=中
执行命令=codex status; codex --version; codex --help; dotnet build ClassroomToolkit.sln -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"; powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=build 0 error; 全量测试 3052 通过; contract/invariant 24 通过; hotspot PASS; 新增 InkRecoveryDiagnosticsPolicyTests、InkSidecarLoadAdmissionPolicyTests 与 InkDirtyPageCoordinator 路径归一化用例通过
回滚动作=git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Export.cs src/ClassroomToolkit.App/Paint/InkSidecarLoadAdmissionPolicy.cs src/ClassroomToolkit.App/Paint/InkDirtyPageCoordinator.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs src/ClassroomToolkit.App/Paint/InkRecoveryDiagnosticsPolicy.cs tests/ClassroomToolkit.Tests/InkSidecarLoadAdmissionPolicyTests.cs tests/ClassroomToolkit.Tests/InkDirtyPageCoordinatorTests.cs tests/ClassroomToolkit.Tests/InkRecoveryDiagnosticsPolicyTests.cs docs/change-evidence/20260401-crosspage-clearall-sidecar-guard.md

platform_na.type=platform_na
platform_na.reason=`codex status` 在非交互终端返回 `stdin is not a terminal`，无法输出加载链
platform_na.alternative_verification=`codex --version` 与 `codex --help` 成功，用于补证平台可用性
platform_na.evidence_link=docs/change-evidence/20260401-crosspage-clearall-sidecar-guard.md
platform_na.expires_at=2026-04-08
