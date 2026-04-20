# 20260420-touch-first-low-occlusion-controls-design

- rule_id: `R1 R2 R6 R8`
- risk_level: `low`
- scope: `Touch-first low-occlusion interaction design spec only`

## 依据
- 用户确认：工具条与悬浮球必须保持简洁、低遮挡；不接受通过增加大量常驻按钮来换取可发现性。
- 已确认方向：以 `再次点按当前已选工具展开本地设置` 作为主模式；全局 `...` 仅承载低频跨工具动作。

## 变更落点
- `docs/superpowers/specs/2026-04-20-touch-first-low-occlusion-controls-design.md`

## 命令与证据
- cmd: `codex --version`
  - exit_code: `0`
  - key_output: `codex-cli 0.121.0`
  - timestamp: `2026-04-20`
- cmd: `codex --help`
  - exit_code: `0`
  - key_output: `Codex CLI usage printed`
  - timestamp: `2026-04-20`
- cmd: `codex status`
  - exit_code: `1`
  - key_output: `Error: stdin is not a terminal`
  - timestamp: `2026-04-20`
- cmd: `Select-String -Path docs/superpowers/specs/2026-04-20-touch-first-low-occlusion-controls-design.md -Pattern 'TODO|TBD|FIXME|XXX'`
  - exit_code: `0`
  - key_output: `no placeholder markers found`
  - timestamp: `2026-04-20`
- cmd: `git diff -- docs/superpowers/specs/2026-04-20-touch-first-low-occlusion-controls-design.md`
  - exit_code: `0`
  - key_output: `spec content reviewed; only new design document added`
  - timestamp: `2026-04-20`

## N/A 记录
- type: `platform_na`
  - reason: `codex status 在非交互终端返回 stdin is not a terminal`
  - alternative_verification: `使用 codex --version 与 codex --help 补足最小诊断矩阵`
  - evidence_link: `docs/change-evidence/20260420-touch-first-low-occlusion-controls-design.md`
  - expires_at: `2026-05-20`
- type: `gate_na`
  - reason: `本次仅新增规格与证据文档，属于纯文档改动，不涉及实现代码`
  - alternative_verification: `执行规格自检（占位词扫描、全文审阅、git diff 人工复核）`
  - evidence_link: `docs/change-evidence/20260420-touch-first-low-occlusion-controls-design.md`
  - expires_at: `2026-05-20`

## 回滚动作
1. `git rm docs/superpowers/specs/2026-04-20-touch-first-low-occlusion-controls-design.md`
2. `git rm docs/change-evidence/20260420-touch-first-low-occlusion-controls-design.md`
3. 确认工作区仅回退本次文档变更。
