# 2026-03-28-rules-six-files-governance.md

- 规则ID: R1,R2,R3,R4,R5,R6,R7,R8,R9,R10,R11,R12
- 影响模块: 全局三文件 + 项目三文件
- 当前落点: GlobalUser/AGENTS.md, GlobalUser/CLAUDE.md, GlobalUser/GEMINI.md, AGENTS.md, CLAUDE.md, GEMINI.md
- 目标归宿: 三层结构稳定化（A共性基线/B平台差异/C项目差异）
- 迁移批次: 2026-03-28-v8.00-v3.10
- 风险等级: 中风险（治理规则调整，非业务代码）

## 执行命令
1. `Get-Content -Raw` 读取六文件现状。
2. `git diff --no-index` 对比同层三文件差异范围。
3. `rg -n` 扫描跨平台错配关键词（codex status/AGENTS.override.md）。
4. `Set-Content` 写入六文件更新版本。
5. `git diff --no-index` 再次校验差异仅限标题/B段/继承指向。

## 验证证据
- `GlobalUser/*` 三文件：A/C/D 一致，B 分别为 Codex/Claude/Gemini 差异。
- 项目级三文件：A/C/D 一致，B 分别为 Codex/Claude/Gemini 差异。
- Claude/Gemini 文件中已移除 Codex 专属诊断命令与路径条款。
- 六文件行数由约 97-108 行压缩到约 94-102 行，冗余降低。

## 回滚动作
- 回滚触发条件: 新规则导致执行歧义、平台加载冲突、门禁无法落地。
- 回滚方式: 恢复六文件到上一版（v7.50/v3.00 文本）。
- 回滚后复验命令:
  - `rg -n "B\. 平台差异|A\.2 R1-R12|Execution Contract" GlobalUser/*.md *.md`
  - `git diff --no-index -- GlobalUser/AGENTS.md GlobalUser/CLAUDE.md`
  - `git diff --no-index -- AGENTS.md CLAUDE.md`

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
