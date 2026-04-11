# Skill Trigger Eval Runbook (Minimum Flow)

## Goal
- Generate `.governance/skill-candidates/trigger-eval-summary.json` with stable validation metrics.
- Unblock `create` action in `promote-skill-candidates.ps1` when `require_trigger_eval_for_create=true`.

## Inputs
- Trigger eval run log: `.governance/skill-candidates/trigger-eval-runs.jsonl`
- Sample template: `.governance/skill-candidates/trigger-eval-runs.sample.jsonl`

## 3-command flow
1. Record one run sample (manual or evidence-driven):
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/governance/register-skill-trigger-eval-run.ps1 `
  -Query "windows powershell output appears as garbled chinese text" `
  -ShouldTrigger $true `
  -Split validation `
  -SkillName custom-auto-pwsh-encoding-mojibake-l-a9b049cd `
  -Triggered true
```

2. Build eval summary:
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/governance/check-skill-trigger-evals.ps1 `
  -RepoRoot . `
  -AsJson
```

3. Run promotion with create gate:
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/governance/promote-skill-candidates.ps1 `
  -AsJson
```

## Passing criteria for create gate
- `validation_pass_rate >= trigger_eval_min_validation_pass_rate`
- `validation_false_trigger_rate <= trigger_eval_max_validation_false_trigger_rate`
- Summary file exists at policy path: `trigger_eval_summary_relative_path`

## Troubleshooting
- `eval_summary_missing`: run `check-skill-trigger-evals.ps1` first.
- `eval_summary_missing_metrics`: ensure each jsonl line includes `query`, `should_trigger`, `triggered`.
- `no_validation_split`: add records with `split=validation`.
- High false trigger rate: add more `should_trigger=false` near-miss queries and improve skill description boundaries.
