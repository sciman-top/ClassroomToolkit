# 20260422 ImageManager StaticResource Hotfix

- date: 2026-04-22
- issue_id: image-manager-xamlparse-staticresource-gradient-teal
- risk_level: low
- active_rule_path: D:\CODE\ClassroomToolkit\AGENTS.md
- current_boundary: fix one missing XAML StaticResource introduced in ImageManagerWindow touch-density cleanup
- target_destination: restore ImageManagerWindow startup by using an existing theme resource and add a regression assertion
- clarification_mode: direct_fix
- attempt_count: 1

## Basis
- user reported runtime `System.Windows.Markup.XamlParseException` from `StaticResourceExtension` at line 105, position 37.
- local line mapping resolved the failing area to `src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml` around the `添加收藏` button.
- static search showed `Gradient_Teal` did not exist in repository resources.
- `Gradient_Success` exists in `src/ClassroomToolkit.App/Assets/Styles/Colors.xaml` and matches the previous `Style_Button_Teal` visual intent.

## Changes
1. Replaced missing resource:
   - from: `Background="{StaticResource Gradient_Teal}"`
   - to: `Background="{StaticResource Gradient_Success}"`
   - file: `src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml`
2. Added regression assertions:
   - `ImageManagerTouchFlowContractTests` now requires `Gradient_Success`
   - `ImageManagerTouchFlowContractTests` now rejects `Gradient_Teal`

## Commands
1. `rg -n "Gradient_Teal|Gradient_Success|Style_ImageManagerToolbarButton" src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml src/ClassroomToolkit.App/Assets/Styles/Colors.xaml`
2. ImageManager static resource scan:
   - collected `StaticResource` references from `ImageManagerWindow.xaml`
   - collected `x:Key` values from app style dictionaries plus local window resources
   - compared refs against keys
3. `rg -n "Gradient_Teal" src tests docs; if ($LASTEXITCODE -eq 1) { 'No Gradient_Teal references remain' } elseif ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }`
4. `dotnet --list-sdks; dotnet --version`
5. `dotnet build ClassroomToolkit.sln -c Debug`
6. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ImageManagerTouchFlowContractTests|FullyQualifiedName~ManagementWindowsXamlContractTests"`
7. `codex --version`
8. `codex --help`
9. `codex status`

## Key Output
- `ImageManagerWindow StaticResource scan passed: 44 references, 296 available keys`
- `No Gradient_Teal references remain`
- SDKs visible now:
  - `8.0.420 [C:\Program Files\dotnet\sdk]`
  - `10.0.202 [C:\Program Files\dotnet\sdk]`
  - `dotnet --version`: `10.0.202`
- Codex diagnostics:
  - `codex --version`: `codex-cli 0.122.0`
  - `codex --help`: succeeded
  - `codex status`: failed with `stdin is not a terminal`

## Gate Status
- build: gate_na
  - reason: repository `global.json` requires SDK `10.0.201` with `rollForward: latestPatch`; current machine exposes `10.0.202`, which is a different feature band and is rejected by SDK resolution.
  - alternative_verification: static resource key scan for `ImageManagerWindow.xaml` passed; missing `Gradient_Teal` reference removed; exact failing line now references existing `Gradient_Success`.
  - evidence_link: `docs/change-evidence/20260422-image-manager-staticresource-hotfix.md`
  - expires_at: when SDK `10.0.201` is installed or `global.json` is intentionally updated by a separate approved change.
- test: gate_na
  - reason: same SDK resolution blocker as build.
  - alternative_verification: targeted contract assertion added but not executable in current SDK state; static search confirms assertion condition manually.
  - evidence_link: `docs/change-evidence/20260422-image-manager-staticresource-hotfix.md`
  - expires_at: when SDK `10.0.201` is installed or `global.json` is intentionally updated by a separate approved change.
- contract/invariant: gate_na
  - reason: same SDK resolution blocker as build.
  - alternative_verification: no architecture/interop files changed in this hotfix; touched files are ImageManager XAML and ImageManager touch-flow contract test only.
  - evidence_link: `docs/change-evidence/20260422-image-manager-staticresource-hotfix.md`
  - expires_at: when SDK `10.0.201` is installed or `global.json` is intentionally updated by a separate approved change.
- hotspot: passed
  - reviewed line 105-108 in `ImageManagerWindow.xaml`; all referenced resources now exist.

## Rollback
1. revert `src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml`
2. revert `tests/ClassroomToolkit.Tests/ImageManagerTouchFlowContractTests.cs`

## Residual Risk
- runtime confirmation still depends on relaunching the app on a machine with the expected SDK/runtime setup.
- after SDK `10.0.201` is restored, rerun the normal gate chain: build -> test -> contract/invariant -> hotspot.
