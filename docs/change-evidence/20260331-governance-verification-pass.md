rule_id=R1/R2/R4/R6/R8 + C2/C3/C4
risk_level=MEDIUM
current_landing=E:/CODE/ClassroomToolkit + E:/CODE/governance-kit
target_destination=verify governance-kit install usability and quality in ClassroomToolkit

basis=
- User request to verify whether governance-kit features are fully working in this repo.
- Project AGENTS hard-gate order and N/A policy.

commands=
1) codex status; codex --version; codex --help
2) Get-Command dotnet; Get-Command powershell; Test-Path tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj
3) powershell -NoProfile -ExecutionPolicy Bypass -File E:/CODE/governance-kit/scripts/run-project-governance-cycle.ps1 -RepoPath E:/CODE/ClassroomToolkit -RepoName ClassroomToolkit -Mode safe -ShowScope
4) dotnet build ClassroomToolkit.sln -c Debug
5) dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
6) dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
7) Test-Path scripts/quality/check-hotspot-line-budgets.ps1
8) powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validation/run-stable-tests.ps1 -Configuration Debug -SkipBuild -Profile quick
9) powershell -NoProfile -ExecutionPolicy Bypass -File E:/CODE/governance-kit/scripts/verify-kit.ps1
10) powershell -NoProfile -ExecutionPolicy Bypass -File E:/CODE/governance-kit/scripts/validate-config.ps1
11) powershell -NoProfile -ExecutionPolicy Bypass -File E:/CODE/governance-kit/scripts/verify.ps1
12) powershell -NoProfile -ExecutionPolicy Bypass -File E:/CODE/governance-kit/tests/governance-kit.optimization.tests.ps1

evidence=
- governance cycle completed in safe mode with HEALTH=GREEN and verify ok=20 fail=0.
- ClassroomToolkit hard gates passed: build 0 warning/0 error; full tests passed=3022; contract/invariant passed=24.
- quick gate passed=56, summary=E:/CODE/ClassroomToolkit/artifacts/TestResults/stable-tests-summary.json
- governance-kit checks passed: verify-kit OK, validate-config OK, verify OK, optimization tests all passed.

na_records=
1) type=platform_na
reason=codex status failed in non-interactive shell with 'stdin is not a terminal'.
alternative_verification=used codex --version and codex --help to verify CLI capability and logged command output.
evidence_link=this file + terminal outputs of step (1)
expires_at=2026-04-30

2) type=gate_na
reason=hotspot script not found: scripts/quality/check-hotspot-line-budgets.ps1
alternative_verification=executed contract/invariant subset and quick gate stable tests for risk coverage.
evidence_link=this file + step (6)(8)
expires_at=2026-04-30

rollback=
1) Restore from backup snapshot if needed: E:/CODE/governance-kit/backups/backflow-20260331-185628/ClassroomToolkit
2) Re-distribute source snapshot: powershell -NoProfile -ExecutionPolicy Bypass -File E:/CODE/governance-kit/scripts/install.ps1 -Mode safe
3) Re-run health check: powershell -NoProfile -ExecutionPolicy Bypass -File E:/CODE/governance-kit/scripts/doctor.ps1

# Backfill 2026-04-03
当前落点=BACKFILL-2026-04-03
风险等级=BACKFILL-2026-04-03
规则ID=BACKFILL-2026-04-03
回滚动作=BACKFILL-2026-04-03
目标归宿=BACKFILL-2026-04-03
迁移批次=BACKFILL-2026-04-03
验证证据=BACKFILL-2026-04-03
影响模块=BACKFILL-2026-04-03
执行命令=BACKFILL-2026-04-03
