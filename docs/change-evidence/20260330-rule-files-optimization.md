# 20260330-rule-files-optimization

- 规则ID=R1/R2/R3/R4/R5/R6/R7/R8,E1,E2,E3,E4,E5,E6
- 影响模块=GlobalUser 规则层 + 项目根规则层
- 当前落点=GlobalUser/{AGENTS,CLAUDE,GEMINI}.md + /{AGENTS,CLAUDE,GEMINI}.md
- 目标归宿=三层职责清晰（共性基线+平台差异+项目差异）、平台差异明确、文本精简且自包含
- 迁移批次=P1
- 风险等级=Low（文档治理变更，不改业务代码）

## 依据
- 用户需求：统一评估并优化 6 份规则文件，要求职责边界清晰、1+1>2 协同、精简不冗余。
- 项目约束：仓库 `AGENTS.md` 与 `GlobalUser/AGENTS.md` 的承接关系与门禁语义。

## 执行命令
- `Get-ChildItem -Force`
- `Get-ChildItem -Force GlobalUser`
- `rg --files -g "*agents*.md" -g "*AGENTS*.md" -g "*claude*.md" -g "*gemini*.md"`
- `Get-Content -Raw GlobalUser/AGENTS.md`
- `Get-Content -Raw GlobalUser/CLAUDE.md`
- `Get-Content -Raw GlobalUser/GEMINI.md`
- `Get-Content -Raw AGENTS.md`
- `Get-Content -Raw CLAUDE.md`
- `Get-Content -Raw GEMINI.md`
- `git --no-pager diff --no-index -- GlobalUser/AGENTS.md GlobalUser/CLAUDE.md`
- `git --no-pager diff --no-index -- AGENTS.md CLAUDE.md`

## 验证证据
- 结构验证：6 份文件均保持 `1 / A / B / C / D`。
- 边界验证：全局文件仅定义 WHAT 与平台通用策略；项目文件仅定义本仓 WHERE/HOW。
- 平台差异验证：B 段按 Codex/Claude/Gemini 分别给出加载链、最小诊断、回退。
- 自包含验证：每个项目文件均显式承接对应 `GlobalUser/*.md`，且单文件可执行本层职责。
- 精简验证：去除重复叙述，统一最低字段键，保留必须信息。

## R6/N/A 说明
- `build/test/contract/invariant/hotspot = N/A`
- `reason`=本次仅规则文档治理，未改任何代码/配置/依赖。
- `alternative_verification`=执行结构校验、承接映射校验、平台差异校验、字段键一致性校验。
- `evidence_link`=`docs/change-evidence/20260330-rule-files-optimization.md`

## 回滚动作
1. `git checkout -- GlobalUser/AGENTS.md GlobalUser/CLAUDE.md GlobalUser/GEMINI.md AGENTS.md CLAUDE.md GEMINI.md`
2. 重新核对承接映射与平台差异段是否恢复至前一版本。

## Waiver
- owner=N/A
- expires_at=N/A
- status=not_used
- recovery_plan=N/A
- evidence_link=docs/change-evidence/20260330-rule-files-optimization.md