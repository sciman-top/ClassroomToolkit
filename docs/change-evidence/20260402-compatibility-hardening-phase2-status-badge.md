# 变更证据：20260402 兼容性增强 Phase2（兼容状态灯接入诊断）

当前落点=系统诊断接入 startup compatibility 状态灯（正常/降级/阻断）与问题统计输出
目标归宿=让一线教师/运维在诊断面板可直接识别当前兼容运行级别，降低误判与排障成本
风险等级=低

执行命令=dotnet build ClassroomToolkit.sln -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"; powershell -File scripts/quality/check-hotspot-line-budgets.ps1; codex status; codex --version; codex --help
验证证据=build 0 error; full test 3085/3085 pass; contract 24/24 pass; hotspot PASS; codex --version=codex-cli 0.118.0; codex --help printed usage

platform_na=cmd: codex status | reason: stdin is not a terminal（非交互终端限制） | alternative_verification: codex --version + codex --help + active_rule_path=D:/OneDrive/CODE/ClassroomToolkit/AGENTS.md | evidence_link: docs/change-evidence/20260402-compatibility-hardening-phase2-status-badge.md | expires_at: 2026-05-02

变更文件=
- src/ClassroomToolkit.App/Diagnostics/StartupCompatibilityStatusPolicy.cs
- src/ClassroomToolkit.App/Diagnostics/DiagnosticsResult.cs
- src/ClassroomToolkit.App/Diagnostics/SystemDiagnostics.cs
- tests/ClassroomToolkit.Tests/StartupCompatibilityStatusPolicyTests.cs

回滚动作=git restore --source=HEAD~1 -- src/ClassroomToolkit.App/Diagnostics/StartupCompatibilityStatusPolicy.cs src/ClassroomToolkit.App/Diagnostics/DiagnosticsResult.cs src/ClassroomToolkit.App/Diagnostics/SystemDiagnostics.cs tests/ClassroomToolkit.Tests/StartupCompatibilityStatusPolicyTests.cs docs/change-evidence/20260402-compatibility-hardening-phase2-status-badge.md

# Backfill 2026-04-03
规则ID=BACKFILL-2026-04-03
迁移批次=BACKFILL-2026-04-03
影响模块=BACKFILL-2026-04-03
