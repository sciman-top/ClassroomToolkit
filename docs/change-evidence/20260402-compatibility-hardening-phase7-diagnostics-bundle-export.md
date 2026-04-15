# 变更证据：20260402 兼容性增强 Phase7（一键诊断包导出）

当前落点=诊断窗口新增“导出诊断包”能力，自动打包 settings + startup report + recent error logs + 当前诊断摘要
目标归宿=现场问题可一键留痕并回传，减少人工收集成本和信息缺失
风险等级=低

执行命令=dotnet build ClassroomToolkit.sln -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"; powershell -File scripts/quality/check-hotspot-line-budgets.ps1; codex status; codex --version; codex --help
验证证据=build 0 error; full test 3087/3087 pass; contract 24/24 pass; hotspot PASS; codex --version=codex-cli 0.118.0; codex --help printed usage

platform_na=cmd: codex status | reason: stdin is not a terminal（非交互终端限制） | alternative_verification: codex --version + codex --help + active_rule_path=D:/OneDrive/CODE/ClassroomToolkit/AGENTS.md | evidence_link: docs/change-evidence/20260402-compatibility-hardening-phase7-diagnostics-bundle-export.md | expires_at: 2026-05-02

变更文件=
- src/ClassroomToolkit.App/Diagnostics/DiagnosticsBundleExportService.cs
- src/ClassroomToolkit.App/Diagnostics/DiagnosticsDialog.xaml
- src/ClassroomToolkit.App/Diagnostics/DiagnosticsDialog.xaml.cs
- tests/ClassroomToolkit.Tests/DiagnosticsBundleExportServiceTests.cs

回滚动作=git restore --source=HEAD~1 -- src/ClassroomToolkit.App/Diagnostics/DiagnosticsBundleExportService.cs src/ClassroomToolkit.App/Diagnostics/DiagnosticsDialog.xaml src/ClassroomToolkit.App/Diagnostics/DiagnosticsDialog.xaml.cs tests/ClassroomToolkit.Tests/DiagnosticsBundleExportServiceTests.cs docs/change-evidence/20260402-compatibility-hardening-phase7-diagnostics-bundle-export.md

# Backfill 2026-04-03
规则ID=BACKFILL-2026-04-03
迁移批次=BACKFILL-2026-04-03
影响模块=BACKFILL-2026-04-03
