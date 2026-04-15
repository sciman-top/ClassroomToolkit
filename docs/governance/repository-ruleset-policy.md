# Repository Ruleset Policy (Observe Phase)

status=observe
baseline=GitHub repository rulesets
owner=repo-maintainers

## Goal

Keep branch and pull-request protection controls explicit, reviewable, and reproducible across repositories.

## Minimum Controls

- Require pull requests before merge to protected branches.
- Require status checks before merge.
- Require at least one explicit review.
- Require all review conversations resolved before merge.

## Evidence

- Policy artifact: `.governance/repository-ruleset-policy.json`
- Optional config artifact: `.github/rulesets/default.json` or `.governance/repository-ruleset-config.json`
- Verification trace in recurring review / doctor outputs.

## Exit Criteria To Enforce

- Ruleset config artifact is present and mapped for all managed repos.
- Recurring review reports stable `PASS` for ruleset baseline checks.
- No high-risk bypass in release branches during observe window.

