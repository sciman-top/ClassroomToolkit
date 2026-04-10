规则ID=R1,R4,R6,R8
影响模块=README.md, README.en.md, 使用指南.md, .gitignore, Git 跟踪清单, GitHub 仓库元信息
当前落点=仓库根文档与 Git 发布治理
目标归宿=中英文仓库入口文档、忽略规则、远端仓库展示信息
迁移批次=2026-04-04-docs-publishing-hygiene
风险等级=低
执行命令=
- codex status
- codex --version
- codex --help
- git status --short
- git remote -v
- git ls-files AGENTS.md CLAUDE.md GEMINI.md .geminiignore .claude .codex .crossnote .vs
- git diff -- README.md README.en.md .gitignore 使用指南.md
- git rm -r --cached .crossnote
- git rm --cached .geminiignore
- git add README.md README.en.md .gitignore 使用指南.md docs/change-evidence/20260404-readme-english-gitignore-hygiene.md
- git commit -m "优化仓库文档并补齐英文版"
- git remote add origin https://github.com/sciman-top/ClassroomToolkit.git
- git push -u origin main
- powershell -Command "Invoke-RestMethod ..."
验证证据=
- platform_na:
  - cmd: codex status
  - exit_code: 1
  - key_output: stdin is not a terminal
  - timestamp: 2026-04-04
  - active_rule_path: E:\CODE\ClassroomToolkit\AGENTS.md
- codex_version:
  - cmd: codex --version
  - exit_code: 0
  - key_output: codex-cli 0.118.0
  - timestamp: 2026-04-04
- gate_na:
  - reason: 本次仅修改文档、忽略规则和 Git 跟踪清单，不涉及可执行代码与运行时契约
  - alternative_verification: 审查 git diff、README 链接与远端配置，仅提交本次文档治理文件
  - evidence_link: docs/change-evidence/20260404-readme-english-gitignore-hygiene.md
  - expires_at: 2026-04-04
- repo_target:
  - repository_full_name: sciman-top/ClassroomToolkit
  - clone_url: https://github.com/sciman-top/ClassroomToolkit.git
回滚动作=
- git revert <doc-commit-sha>
- 如需恢复本地专用文件跟踪：git restore --staged .crossnote .geminiignore 后重新评估 .gitignore

# Backfill 2026-04-03
执行命令=backfill-evidence-template-fields.ps1
验证证据=template-field-backfill-2026-04-03
回滚动作=git revert evidence backfill commit
