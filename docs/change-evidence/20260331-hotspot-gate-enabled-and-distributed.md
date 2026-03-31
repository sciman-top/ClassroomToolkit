rule_id=R1/R2/R4/R6/R8 + C2/C3/C4/C6/C8
risk_level=MEDIUM
current_landing=E:/CODE/ClassroomToolkit + E:/CODE/governance-kit
target_destination=enable executable hotspot gate and make it distributable via governance-kit

basis=
- Previous hotspot gate relied on gate_na due missing script.
- Need to keep hard gate order with executable hotspot step and preserve governance source-of-truth consistency.

changes=
1) Added hotspot gate implementation:
   - scripts/quality/check-hotspot-line-budgets.ps1
   - scripts/quality/hotspot-line-budgets.json
2) Updated project rule docs (AGENTS/CLAUDE/GEMINI):
   - hotspot command switched from gate_na to executable script.
   - hotspot script path field set to scripts/quality/check-hotspot-line-budgets.ps1.
3) Backflow + governance distribution fixes:
   - governance-kit config/project-custom-files.json adds two quality files for ClassroomToolkit.
   - governance-kit config/targets.json adds two target mappings.
   - governance-kit source/project/ClassroomToolkit/custom/scripts/quality/* added.
4) Workspace hygiene optimization in governance-kit:
   - added E:/CODE/governance-kit/.gitignore to ignore .gitmessage.txt and backups/backflow-*/

commands=
1) dotnet build ClassroomToolkit.sln -c Debug
2) dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
3) dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
4) powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1
5) powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validation/run-stable-tests.ps1 -Configuration Debug -SkipBuild -Profile quick
6) powershell -NoProfile -ExecutionPolicy Bypass -File E:/CODE/governance-kit/scripts/backflow-project-rules.ps1 -RepoPath E:/CODE/ClassroomToolkit -RepoName ClassroomToolkit -Mode safe
7) powershell -NoProfile -ExecutionPolicy Bypass -File E:/CODE/governance-kit/scripts/install.ps1 -Mode safe
8) powershell -NoProfile -ExecutionPolicy Bypass -File E:/CODE/governance-kit/scripts/verify.ps1
9) powershell -NoProfile -ExecutionPolicy Bypass -File E:/CODE/governance-kit/scripts/doctor.ps1

evidence=
- ClassroomToolkit hard gates passed: build(0 warning/0 error), full tests=3022 passed, contract/invariant=24 passed.
- hotspot gate now executable and passing: status=PASS, 15 budget entries all within limits.
- quick gate passed=56; summary=E:/CODE/ClassroomToolkit/artifacts/TestResults/stable-tests-summary.json
- governance-kit validate/install/verify/doctor all passed after config update.
- verify target count increased from 20 to 22 and includes quality script/json mappings.
- final doctor summary: HEALTH=GREEN.

na_records=
1) type=platform_na
reason=codex status failed in non-interactive shell with 'stdin is not a terminal'.
alternative_verification=codex --version and codex --help executed successfully.
evidence_link=20260331-governance-verification-pass.md + terminal logs
expires_at=2026-04-30

rollback=
1) Remove new hotspot gate files and revert AGENTS/CLAUDE/GEMINI hotspot entries in ClassroomToolkit.
2) Revert governance-kit config updates in config/project-custom-files.json and config/targets.json.
3) Restore from latest backflow snapshot under E:/CODE/governance-kit/backups/backflow-*/ClassroomToolkit if needed.
4) Re-run install/verify/doctor to confirm rollback health state.
