# 20260330-rule-files-optimization-p3

- 规则ID=R1,R2,R3,R4,R5,R6,R7,R8,E1,E2,E3,E4,E5,E6
- 影响模块=GlobalUser 规则层 + 项目根规则层
- 当前落点=GlobalUser/{AGENTS,CLAUDE,GEMINI}.md + /{AGENTS,CLAUDE,GEMINI}.md
- 目标归宿=去除轻度过度设计，保留核心治理语义与执行可操作性
- 迁移批次=P3
- 风险等级=Low（纯规则文档治理）

## 本轮是否过度优化/过度设计
- 结论：P2 存在轻度过度设计风险（治理术语密度偏高）。
- 处理：P3 已精简冗余描述，保留硬门禁、失败分流、承接映射、证据字段。

## 执行命令
- `Set-Content` 覆写 6 份规则文件。
- `rg -n "^## (1\.|A\.|B\.|C\.|D\.)|承接 `GlobalUser/|codex status|claude --version|gemini --version|R6|N/A" ...`
- 行数与字节统计校验。

## 验证证据
- 6 文件均保留 `1 / A / B / C / D` 结构。
- 全局文件保持 WHAT + 平台差异；项目文件保持 WHERE/HOW。
- 平台差异命令均保留且互斥清晰。
- 文本继续收敛：全局约 73 行/份，项目约 85 行/份。

## R6/N/A 说明
- `build/test/contract/invariant/hotspot = N/A`
- `reason`=本轮仅文档治理优化，未改代码/依赖/配置。
- `alternative_verification`=结构校验、承接校验、平台差异校验、字段一致性校验。
- `evidence_link`=`docs/change-evidence/20260330-rule-files-optimization-p3.md`

## 回滚动作
1. 恢复 6 份规则文件到 P2 或 P1 版本。
2. 重新执行结构与承接校验并留痕。

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
