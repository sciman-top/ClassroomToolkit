# GitHub Presence Playbook

## Objective
Make the repository easier to discover, easier to trust, and faster to try.

## Repository page
- Repository name should describe the user outcome, not internal architecture.
- Description should say what it does, who it is for, and why it is worth trying.
- Topics should match search terms users would actually type.
- Pin the most useful entry points: demo, docs, sample repo, and the main project.
- Link Releases, Docs, and Demo from the repository description or README.

## README conversion
- The first screen should answer: what is this, who is it for, why now, and how do I start.
- Keep the first runnable path under five minutes.
- Include one minimal example that proves the value quickly.
- Add screenshots, GIFs, or sample output if the flow is not obvious.
- Add FAQ and troubleshooting sections that remove the most common trial blockers.

## Release conversion
- Release notes should describe user-visible outcomes, not commit history.
- Each release should say what changed, what to try, and whether upgrade is safe.
- Put breaking changes and rollback guidance where users can see them immediately.

## Trial path
- Prefer a demo, sample repo, docker compose, or starter data set.
- Make the first success path obvious from the README and from the release page.
- State dependencies and permissions before the user hits them.

## AI coding notes
- When Codex CLI or another AI agent edits the repository, keep README, release notes, and the playbook in sync.
- If a user-facing flow changes, update the README first, then the release template, then the demo or example assets.
- Treat this file as the reference checklist for GitHub-facing changes.

## Minimum checklist
- README explains the project in one sentence.
- Quick Start works from a clean clone.
- Demo or examples are visible without hunting.
- Release template exists and includes upgrade guidance.
- Security contact or reporting path exists.
