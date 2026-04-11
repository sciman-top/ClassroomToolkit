# SSDF Mapping (Observe Phase)

status=observe
framework=NIST SP 800-218 SSDF
owner=repo-maintainers

## Scope

This mapping tracks how repository controls align to SSDF practices during observe phase.

## Mapping Snapshot

- PO (Prepare the Organization): governance policy files and rule distribution controls
- PS (Protect the Software): hooks, CI gates, repository policy checks
- PW (Produce Well-Secured Software): build/test/contract/hotspot hard gate sequence
- RV (Respond to Vulnerabilities): evidence trail, waiver lifecycle, recurring reviews

## Evidence

- AGENTS governance rules
- quality-gate workflows and local hook controls
- `docs/change-evidence/` records with rollback path
- recurring review outputs and alert snapshots

## Next Step To Enforce

- Add per-practice measurable KPIs and mandatory pass thresholds.
