规则ID=R1,R2,R4,R6,R8,E1,E2
影响模块=AGENTS.md; CLAUDE.md; GEMINI.md
当前落点=D:/OneDrive/CODE/ClassroomToolkit 项目级规则文档
目标归宿=D:/OneDrive/CODE/repo-governance-hub/source/project/ClassroomToolkit/* 并与目标仓保持一致
迁移批次=2026-03-30-project-rules-open-rewrite
风险等级=LOW(doc-only)
执行命令=1) 开放式重写 D:/OneDrive/CODE/ClassroomToolkit/{AGENTS,CLAUDE,GEMINI}.md；2) powershell -File D:/OneDrive/CODE/repo-governance-hub/scripts/backflow-project-rules.ps1 -RepoPath D:/OneDrive/CODE/ClassroomToolkit -RepoName ClassroomToolkit -Mode safe；3) powershell -File D:/OneDrive/CODE/repo-governance-hub/scripts/doctor.ps1；4) powershell -File D:/OneDrive/CODE/ClassroomToolkit/scripts/validation/run-stable-tests.ps1 -Configuration Debug -SkipBuild -Profile quick
验证证据=三份文档版本升级为 3.74（2026-03-30）；backflow 备份=D:/OneDrive/CODE/repo-governance-hub/backups/backflow-20260330-190338/ClassroomToolkit；verify ok=11 fail=0；doctor HEALTH=GREEN；stable-tests quick 通过=56，摘要=D:/OneDrive/CODE/ClassroomToolkit/artifacts/TestResults/stable-tests-summary.json
回滚动作=1) 从 D:/OneDrive/CODE/repo-governance-hub/backups/backflow-20260330-190338/ClassroomToolkit/source-before 恢复旧版三文档；2) 执行 install.ps1 -Mode safe 再分发；3) 执行 doctor.ps1 + quick stable tests 复验回滚状态
