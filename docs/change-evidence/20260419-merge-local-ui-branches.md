# 2026-04-19 Merge Local UI Branches

- rule_ids: `R1`, `R2`, `R6`, `R8`
- risk_level: `medium`
- boundary: `main` git integration + UI XAML contract surface
- current_landing: `main` with local workspace changes and two divergent local UI branches
- target_destination: clean `main` containing current workspace save point plus merged local UI branches

## Basis

- User request: `先整理工作区，再合并其它分支到main`
- Local branches merged:
  - `feature/ui-best-practice-end-state`
  - `backup/ui-end-state-before-squash-20260414`
- Workspace cleanup excluded generated/local automation outputs via `.gitignore`:
  - `.agent-build/`
  - `.governed-ai/`
  - `**/bin-agent/`

## Commands

1. `codex --version`
2. `codex --help`
3. `codex status`
4. `git status --short --branch`
5. `dotnet build ClassroomToolkit.sln -c Debug`
6. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
7. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`

## Key Output

- `codex --version`: `codex-cli 0.121.0`
- `codex --help`: succeeded
- `codex status`: failed with `stdin is not a terminal`
- pre-merge build: passed, `0` warnings, `0` errors
- pre-merge full test: `3324 passed`
- pre-merge contract/invariant: `28 passed`
- final build: passed, `0` warnings, `0` errors
- final full test: `3327 passed`
- final contract/invariant: `28 passed`

## Platform N/A

- type: `platform_na`
- reason: `codex status` cannot run in this non-interactive shell and returns `stdin is not a terminal`
- alternative_verification: used `codex --version` and `codex --help`, then verified active repository state with `git status --short --branch`
- evidence_link: `docs/change-evidence/20260419-merge-local-ui-branches.md`
- expires_at: `2026-05-19`

## Hotspot Review

- `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml`: removed old branch close buttons and close hint after full test caught a contract regression; current windowed preview closes via image/background click.
- `src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml`: kept current grid layout and added stable minimum button widths from the UI branch.
- `tests/ClassroomToolkit.Tests/App/*Widget*ContractTests.cs`: consolidated duplicated XAML helper logic while preserving strict token value checks and branch-added resource reference checks.
- conclusion: no unresolved merge markers; UI contract and invariant gates pass.

## Rollback

1. Revert merge commit `599037b` if only the backup branch integration must be removed.
2. Revert merge commit `104d314` if the UI best-practice branch integration must be removed.
3. Revert commit `d52564d` if the pre-merge workspace save point must be removed.
