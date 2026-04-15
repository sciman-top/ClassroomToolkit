rule_id=R2/R4/R6/R8 + C2/C4/C6/C8
risk_level=LOW-MEDIUM
current_landing=D:/OneDrive/CODE/ClassroomToolkit + D:/OneDrive/CODE/repo-governance-hub
target_destination=close CI skip gap and simplify local gate execution path

basis=
- azure-pipelines/.gitlab-ci/.github(workflow quality-gates) all referenced scripts/quality/run-local-quality-gates.ps1, but file was missing, causing silent skip.
- Need to avoid hidden gate bypass and avoid unnecessary process layering in gate runner.

changes=
1) Added local quality gate runner:
   - scripts/quality/run-local-quality-gates.ps1
   - supports profile=quick/full
   - executes build -> (test for full) -> contract/invariant -> hotspot -> stable-tests
2) Simplified gate runner implementation:
   - replaced nested external powershell process calls with direct local script invocation for hotspot + stable-tests.
3) Governance source/distribution synchronization:
   - governance-kit config/project-custom-files.json adds scripts/quality/run-local-quality-gates.ps1
   - governance-kit config/targets.json adds mapping for scripts/quality/run-local-quality-gates.ps1
   - source/project/ClassroomToolkit/custom/scripts/quality/run-local-quality-gates.ps1 synced

verification=
- powershell -File scripts/quality/run-local-quality-gates.ps1 -Profile quick -Configuration Debug => pass
- powershell -File scripts/quality/run-local-quality-gates.ps1 -Profile full -Configuration Debug => pass
- governance-kit validate/install/verify/doctor => pass
- final governance health: HEALTH=GREEN; verify ok=23 fail=0

overdesign_assessment=
- Found and fixed one practical overdesign smell: CI had multiple quality-gate entries but relied on a missing script, creating structural complexity without enforcement.
- New runner keeps minimal sequencing and no additional abstraction layers.
- No further immediate over-optimization evidence found in this change scope.

rollback=
1) Remove scripts/quality/run-local-quality-gates.ps1
2) Revert governance-kit config/project-custom-files.json and config/targets.json entries for the script
3) Re-run governance install/verify/doctor to confirm rollback consistency

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
