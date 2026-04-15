规则ID=R1/R2/R4/R6/R8 + E3/E4
影响模块=scripts/quality, scripts/governance, .github/workflows, docs/change-evidence, docs/governance/reports, docs/runbooks
当前落点=D:/OneDrive/CODE/ClassroomToolkit/scripts/{quality,governance}
目标归宿=D:/OneDrive/CODE/repo-governance-hub/source/project/ClassroomToolkit/*
迁移批次=2026-04-03-governance-endstate-p0-p1-p2
风险等级=Medium(新增门禁脚本与编排，完成 evidence all 强制收敛)
执行命令=see_command_list_below
- powershell -NoProfile -ExecutionPolicy Bypass -File scripts/governance/check-waiver-health.ps1
- powershell -NoProfile -ExecutionPolicy Bypass -File scripts/governance/check-evidence-completeness.ps1 -Mode changed -Threshold 98
- powershell -NoProfile -ExecutionPolicy Bypass -File scripts/governance/run-doctor-endstate.ps1 -EvidenceThreshold 98
- powershell -NoProfile -ExecutionPolicy Bypass -File scripts/governance/run-doctor-endstate.ps1 -EvidenceMode all -EvidenceThreshold 98
- powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile quick -Configuration Debug
- dotnet build ClassroomToolkit.sln -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
- powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1
- powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile quick -Configuration Debug -EmitGovernanceReport
- powershell -NoProfile -ExecutionPolicy Bypass -File scripts/governance/run-endstate-loop.ps1 -Profile quick -Configuration Debug -EvidenceMode changed -RunAllEvidenceObserve
- powershell -NoProfile -ExecutionPolicy Bypass -File scripts/governance/backfill-evidence-template-fields.ps1
- powershell -NoProfile -ExecutionPolicy Bypass -File scripts/governance/check-evidence-completeness.ps1 -Mode all -Threshold 98
- powershell -NoProfile -ExecutionPolicy Bypass -File scripts/governance/run-endstate-loop.ps1 -Profile quick -Configuration Debug -EvidenceMode all
- documentation update: docs/runbooks/governance-endstate-maintenance.md
验证证据=see_evidence_list_below
- 新增脚本：
  scripts/governance/check-waiver-health.ps1
  scripts/governance/check-evidence-completeness.ps1
  scripts/governance/run-doctor-endstate.ps1
  scripts/governance/run-endstate-loop.ps1
  scripts/governance/backfill-evidence-template-fields.ps1
- 门禁编排接入：scripts/quality/run-local-quality-gates.ps1 在 hotspot 后追加 waiver-health + evidence-completeness，并支持 -EmitGovernanceReport。
- CI 接入：
  .github/workflows/quality-gate.yml 已提升到 hotspot + waiver + evidence(all) + doctor(all) 强制阻断，并上传 governance reports artifact。
  .github/workflows/quality-gates.yml 使用 -EmitGovernanceReport 并上传 governance reports artifact。
- 硬门禁全链路通过（固定顺序）：
  build pass（0 warning / 0 error）
  test pass（3113）
  contract/invariant pass（24）
  hotspot PASS（15/15 within budget）
- evidence all 收敛：
  check-evidence-completeness -Mode all -Threshold 98 => PASS，files=56，overall_coverage=100。
- doctor 报告：
  PASS(changed): docs/governance/reports/endstate-20260403-012303.{md,json}
  PASS(all): docs/governance/reports/endstate-20260403-012642.{md,json}
  PASS(all): docs/governance/reports/endstate-20260403-012646.{md,json}
  PASS(all): docs/governance/reports/endstate-20260403-012801.{md,json}
  PASS(all): docs/governance/reports/endstate-20260403-012813.{md,json}
  PASS(all): docs/governance/reports/endstate-20260403-012817.{md,json}
- 新增运行手册：
  docs/runbooks/governance-endstate-maintenance.md（终态维护、反过度设计、反过度优化约束）
回滚动作=see_rollback_list_below
- 回滚脚本改动：删除 scripts/governance/*.ps1 并恢复 scripts/quality/run-local-quality-gates.ps1 与 .github/workflows/quality-*.yml 相关调用段。
- 回滚 evidence 补齐：对 docs/change-evidence 的批量补齐提交执行 git revert。
- 若需临时放行：使用 waiver（含 owner/expires_at/recovery_plan/evidence_link）。
- 回灌后统一复验：
  powershell -File D:/OneDrive/CODE/repo-governance-hub/scripts/install.ps1 -Mode safe
  powershell -File D:/OneDrive/CODE/repo-governance-hub/scripts/doctor.ps1
