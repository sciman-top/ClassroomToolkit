## Clarification Trigger (Strict)
- 默认模式: direct_fix
- 自动触发条件(任一满足):
  - 同一 issue_id 连续失败次数 >= 2
  - 目标语义冲突或验收口径冲突
- 提问上限: 3

## Clarification Questions (Pick Top 1-3)
1. 当前行为与期望行为分别是什么?
2. 本轮明确不做什么(非目标)?
3. 必须通过的验收样例是什么?

## Resume Rule
- 澄清结论确认后切回 direct_fix，并清零该 issue_id 失败计数。
