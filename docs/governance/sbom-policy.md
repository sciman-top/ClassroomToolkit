# SBOM Policy (Observe Phase)

status=observe
baseline=SBOM
owner=repo-maintainers

## Goal

Generate and retain software bill of materials (SBOM) artifacts for every main-branch change so dependency provenance can be traced during incident response and release review.

## Minimum Requirements

- Keep `.github/workflows/sbom.yml` enabled.
- Generate SPDX JSON output (`sbom.spdx.json`).
- Keep generated SBOM artifacts for audit windows configured by repository policy.

## Evidence

- Workflow file: `.github/workflows/sbom.yml`
- Latest run artifact: `sbom-spdx`
- Optional local check script: `scripts/quality/run-supply-chain-checks.ps1`

## Exit Criteria To Enforce

- Workflow passes consistently across recent baseline window.
- Release process references SBOM artifact location.
- Incident drill confirms dependency lookup path is usable.
