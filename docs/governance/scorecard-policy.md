# OpenSSF Scorecard Policy (Observe Phase)

status=observe
baseline=OpenSSF Scorecard
owner=repo-maintainers

## Goal

Continuously evaluate repository security posture and supply-chain hygiene with OpenSSF Scorecard, then feed findings into governance remediation loops.

## Minimum Requirements

- Keep `.github/workflows/scorecard.yml` enabled.
- Publish SARIF results to GitHub Security tab.
- Triage non-trivial findings and track remediation in change evidence.

## Evidence

- Workflow file: `.github/workflows/scorecard.yml`
- Latest SARIF upload run in Actions/Security
- Related remediation evidence in `docs/change-evidence/`

## Exit Criteria To Enforce

- Scorecard findings are stable and triaged.
- High-impact findings have owners and recovery plan.
- Governance review includes periodic scorecard trend snapshot.
