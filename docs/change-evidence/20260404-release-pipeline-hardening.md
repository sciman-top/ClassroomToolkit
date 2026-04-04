规则ID=R1/R2/R4/R6/R8
影响模块=scripts/release, .github/workflows, docs/runbooks
当前落点=发布脚本存在硬编码运行时版本与占位发布链接，CI 缺少独立发布流水线
目标归宿=发布链配置集中化（runtime/rid 单一来源）+ 可执行发布工作流 + 发布说明链接自动推导
迁移批次=2026-04-04-release-pipeline-hardening
风险等级=中（发布链脚本与CI行为调整）
执行命令=codex status; codex --version; codex --help; dotnet build ClassroomToolkit.sln -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"; powershell -File scripts/quality/check-hotspot-line-budgets.ps1; powershell -ExecutionPolicy Bypass -File scripts/release/preflight-check.ps1 -SkipTests; powershell -ExecutionPolicy Bypass -File scripts/release/prepare-distribution.ps1 -Version 2026.04.04-smoke -PackageMode standard -SkipPublish -SkipZip -AllowOverwriteVersion
验证证据=build 0 error; test 3171 passed; contract/invariant 25 passed; hotspot PASS; preflight-check passed 并生成 artifacts/release/preflight-compatibility-report.json; prepare-distribution 产出 release-manifest.json 且自动推导 release notes URL
回滚动作=git checkout -- scripts/release/prepare-distribution.ps1 scripts/release/preflight-check.ps1 scripts/release/release-config.json .github/workflows/release-package.yml docs/runbooks/release-prevention-checklist.md docs/change-evidence/20260404-release-pipeline-hardening.md；随后按 build->test->contract/invariant->hotspot 复验
platform_na.reason=codex status 在非交互终端返回 stdin is not a terminal，无法输出交互状态
platform_na.alternative_verification=codex --version 返回 codex-cli 0.118.0；codex --help exit_code=0
platform_na.evidence_link=docs/change-evidence/20260404-release-pipeline-hardening.md
platform_na.expires_at=2026-05-04
gate_na.reason=N/A
gate_na.alternative_verification=N/A
gate_na.evidence_link=docs/change-evidence/20260404-release-pipeline-hardening.md
gate_na.expires_at=N/A
