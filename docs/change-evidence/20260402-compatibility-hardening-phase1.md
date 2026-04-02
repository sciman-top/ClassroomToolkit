# 变更证据：20260402 兼容性增强 Phase1（SLA + 启动探测补强）

当前落点=启动兼容性探测增强（VC++ 运行库探测、PPT/WPS 位数一致性提示）+ 兼容运营文档（SLA、矩阵模板、基线）
目标归宿=提升跨版本环境可观测性与排障效率，减少课堂现场兼容性不确定风险
风险等级=中

执行命令=codex status; codex --version; codex --help; Get-Command dotnet; Get-Command powershell; Test-Path tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj; dotnet build ClassroomToolkit.sln -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"; powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=build 0 error; full test 3082/3082 pass; contract 24/24 pass; hotspot PASS; codex --version=codex-cli 0.118.0; codex --help printed usage

platform_na=cmd: codex status | reason: stdin is not a terminal（非交互终端限制） | alternative_verification: codex --version + codex --help + active_rule_path=E:/CODE/ClassroomToolkit/AGENTS.md | evidence_link: docs/change-evidence/20260402-compatibility-hardening-phase1.md | expires_at: 2026-05-02

变更文件=
- src/ClassroomToolkit.Services/Compatibility/StartupCompatibilityProbe.cs
- tests/ClassroomToolkit.Tests/StartupCompatibilityProbeTests.cs
- docs/compatibility/compatibility-sla.md
- docs/compatibility/matrix-template.md
- docs/compatibility/matrix-baseline-2026Q2.md

回滚动作=git restore --source=HEAD~1 -- src/ClassroomToolkit.Services/Compatibility/StartupCompatibilityProbe.cs tests/ClassroomToolkit.Tests/StartupCompatibilityProbeTests.cs docs/compatibility/compatibility-sla.md docs/compatibility/matrix-template.md docs/compatibility/matrix-baseline-2026Q2.md docs/change-evidence/20260402-compatibility-hardening-phase1.md

# Backfill 2026-04-03
规则ID=BACKFILL-2026-04-03
迁移批次=BACKFILL-2026-04-03
影响模块=BACKFILL-2026-04-03
