# 20260403-safe-autopilot-orchestrator.md
影响模块=scripts/automation/run-safe-autopilot.ps1
当前落点=E:/CODE/ClassroomToolkit
目标归宿=E:/CODE/ClassroomToolkit/scripts/automation/run-safe-autopilot.ps1
迁移批次=2026-04-03-safe-autopilot

执行命令=1) codex status; 2) codex --version; 3) codex --help; 4) powershell -NoProfile -ExecutionPolicy Bypass -File scripts/automation/run-safe-autopilot.ps1 -DryRun -MaxCycles 1; 5) powershell -NoProfile -ExecutionPolicy Bypass -File scripts/automation/run-safe-autopilot.ps1 -MaxCycles 1 -SkipGovernanceCycle -SkipTaskLoop -QualityProfile quick; 6) dotnet build ClassroomToolkit.sln -c Debug; 7) dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug; 8) dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"; 9) powershell -File scripts/quality/check-hotspot-line-budgets.ps1; 10) powershell -NoProfile -ExecutionPolicy Bypass -File E:/CODE/governance-kit/scripts/verify-kit.ps1; 11) powershell -NoProfile -ExecutionPolicy Bypass -File E:/CODE/governance-kit/scripts/validate-config.ps1; 12) powershell -NoProfile -ExecutionPolicy Bypass -File E:/CODE/governance-kit/scripts/verify.ps1; 13) powershell -NoProfile -ExecutionPolicy Bypass -File E:/CODE/governance-kit/scripts/doctor.ps1
验证证据=安全自动编排脚本 dry-run 输出执行顺序；实跑 1 轮（跳过 governance-cycle 与 task-loop）通过 quality gates；本仓硬门禁 build/test/contract/hotspot 全通过；governance-kit verify-kit/validate-config/verify/doctor 全通过，HEALTH=GREEN，无需即时修复
回滚动作=git checkout -- scripts/automation/run-safe-autopilot.ps1 docs/change-evidence/20260403-safe-autopilot-orchestrator.md

platform_na=reason: codex status 在非交互终端失败（stdin is not a terminal） | alternative_verification: 使用 codex --version 与 codex --help 补充平台能力验证 | evidence_link: docs/change-evidence/20260403-safe-autopilot-orchestrator.md | expires_at: 2026-04-30
