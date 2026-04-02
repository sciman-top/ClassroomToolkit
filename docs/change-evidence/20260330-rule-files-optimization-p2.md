# 20260330-rule-files-optimization-p2

- 规则ID=R1,R2,R3,R4,R5,R6,R7,R8,E1,E2,E3,E4,E5,E6
- 影响模块=GlobalUser 规则层 + 项目根规则层
- 当前落点=GlobalUser/{AGENTS,CLAUDE,GEMINI}.md + /{AGENTS,CLAUDE,GEMINI}.md
- 目标归宿=在自包含前提下进一步精简文本并提高平台差异可操作性
- 迁移批次=P2
- 风险等级=Low（纯规则文档治理）

## 依据
- 用户指令：自动连续执行。
- P1 后目标：继续降冗余、保边界、增可维护性。

## 执行命令
- `Set-Content` 覆写 6 份规则文件。
- `rg -n "^## (1\.|A\.|B\.|C\.|D\.)" ...`（结构校验）
- `rg -n "承接 `GlobalUser/|codex status|claude --version|gemini --version" ...`（承接与平台差异校验）
- 行数/字节统计校验（精简度对比）。

## 验证证据
- 6 文件均保留 `1 / A / B / C / D`。
- 全局文件均仅定义 WHAT 与平台差异，不含仓库特有命令。
- 项目文件均承接对应全局文件并保留固定门禁链路与失败分流。
- 文本进一步压缩（全局文件降至约 74 行/份）。

## R6/N/A 说明
- `build/test/contract/invariant/hotspot = N/A`
- `reason`=本轮仅文档治理，未触及代码/依赖/配置。
- `alternative_verification`=执行结构校验、承接校验、平台差异校验、字段一致性校验。
- `evidence_link`=`docs/change-evidence/20260330-rule-files-optimization-p2.md`

## 回滚动作
1. 使用版本控制恢复上述 6 文件到 P1 前版本。
2. 或按 `docs/change-evidence/20260330-rule-files-optimization.md` 的回滚动作恢复。

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
