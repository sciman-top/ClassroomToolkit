规则ID=R4/R6/R8 + platform_na(codex-status-noninteractive)
影响模块=governance 安装链验收、项目硬门禁、规则同步
当前落点=D:/OneDrive/CODE/ClassroomToolkit/{AGENTS.md,CLAUDE.md,GEMINI.md}
目标归宿=D:/OneDrive/CODE/repo-governance-hub/source/project/ClassroomToolkit/
迁移批次=2026-04-02-rules-sync-batch1
风险等级=Medium(受控脚本写入 + 全链路复验)
执行命令=
- codex status (exit=1, stdin is not a terminal)
- codex --version (exit=0, codex-cli 0.118.0)
- codex --help (exit=0)
- dotnet build ClassroomToolkit.sln -c Debug (pass)
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug (pass: 3066)
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests" (pass: 24)
- powershell -File scripts/quality/check-hotspot-line-budgets.ps1 (PASS)
- powershell -File D:/OneDrive/CODE/repo-governance-hub/scripts/backflow-project-rules.ps1 -RepoPath D:/OneDrive/CODE/ClassroomToolkit -RepoName ClassroomToolkit -Mode safe -IncludeCustomFiles $false
- powershell -File D:/OneDrive/CODE/repo-governance-hub/scripts/install.ps1 -Mode safe
验证证据=
- Hash 一致：
  ClassroomToolkit AGENTS/CLAUDE/GEMINI == governance-kit source/project/ClassroomToolkit 同名文件
- 全局 Hash 一致：
  governance-kit source/global/{AGENTS,CLAUDE,GEMINI}.md == C:/Users/sciman/.{codex,claude,gemini}/同名文件
- install verify: ok=23 fail=0
- doctor: HEALTH=GREEN
- platform_na:
  type=platform_na
  reason=codex status 在非交互终端返回 "stdin is not a terminal"
  alternative_verification=codex --version + codex --help + active_rule_path=D:/OneDrive/CODE/ClassroomToolkit/AGENTS.md
  evidence_link=docs/change-evidence/20260402-governance-kit-install-verification.md
  expires_at=2026-04-09
回滚动作=
- 规则同步回滚：使用 governance-kit backups/backflow-20260402-004916/ClassroomToolkit/source-before 与 target-snapshot
- 分发回滚：powershell -File D:/OneDrive/CODE/repo-governance-hub/scripts/restore.ps1 -BackupDir <选定备份目录>

# Backfill 2026-04-03
回滚动作=BACKFILL-2026-04-03
验证证据=BACKFILL-2026-04-03
执行命令=BACKFILL-2026-04-03
