# SLSA Target Level (Observe Phase)

status=observe
framework=SLSA
owner=repo-maintainers

## Current Target

- target_level=SLSA L1 (observe)
- evidence_type=workflow + artifact note + governance review

## Current Controls

- Workflow: `.github/workflows/slsa.yml`
- Artifact note generated on main/release events
- Governance review consumes external baseline status from doctor/recurring review

## Gap To Next Level

- Introduce verifiable provenance attestation pipeline
- Standardize immutable build environment requirements
- Define enforce gate for provenance verification before release
