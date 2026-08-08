# Global rule 9.73 project-contract evidence

- Repository: `ClassroomToolkit`
- Scope: project rule mapping only; no business-code or host-runtime mutation.
- Official basis: current Codex AGENTS loading/precedence and rules semantics; Claude platform delta remains separately verified.
- Git profile: baseline=`main`; upstream=`origin/main`.
- Before AGENTS SHA-256: `609581AFCBB7EDC4D88622CB4CAF2E017A22E7596CBA9D45EF0B7B35DC123877`
- After AGENTS SHA-256: `F74A0B7363F2EDA1ECEEABAE7E5C2F8DD681AA01DBC113B3708438B5052678A5`
- Planned gate: `pwsh -NoProfile -File scripts/quality/run-local-quality-gates.ps1 -Profile standard`
- Current verification: standard gate passed; build, 3508 stable tests, 29 contracts, hotspot and dependency vulnerability scan passed.
- N/A: host loading and live acceptance remain outside repository-static verification.
- Rollback: revert only this repository's `AGENTS.md` and this evidence file to the recorded before hash.
- Truth boundary: `repo_verified=passed`; `host_loaded=codex_fresh_prompt_verified`; `claude_loaded=not_run`; `live_accepted=not_run`.
