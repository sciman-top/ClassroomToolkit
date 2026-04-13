# UI Best Practice End State Evidence

- Date: 2026-04-14
- Scope: WPF window visual unification, compact density normalization, short copy cleanup
- Branch: `feature/ui-best-practice-end-state`

## Goal

Bring the ClassroomToolkit window set to a consistent visual end state without adding runtime-heavy effects or changing business behavior.

## Covered Windows

- Main window
- Roll call window
- Paint settings
- Roll call settings
- Image/PDF manager
- Photo fullscreen / overlay windows
- Student list
- About dialog
- Auto exit dialog
- Class select dialog
- Timer set dialog
- Remote key dialog
- Ink settings dialog
- Diagnostics dialog
- Startup compatibility warning dialog
- Quick color palette
- Board color dialog
- Roll call group overlay

## Main Outcomes

- Unified shared color, radius, spacing, and compact control size tokens
- Replaced most repeated `12/13/14` font sizes with shared typography tokens
- Replaced most repeated `30/34/36/40` control sizes with shared density tokens
- Shortened user-facing copy where the meaning stayed stable
- Preserved existing interaction contracts and behavior-sensitive tooltips

## Intentional Retentions

- `LauncherBubbleWindow.xaml`
  - `56x56` shell and inner circular proportions are intentionally fixed to preserve tray-like floating affordance.
- Fixed widths such as `72`, `80`, `96`, `104`, `132`
  - Retained where they stabilize button labels, footer action groups, or narrow utility dialogs.
- Narrow numeric/value columns such as `Width="40"`
  - Retained where they are semantic value columns, not freeform layout spacing.
- A small number of longer tooltips
  - Retained where tests or behavior contracts depend on exact wording, or where the action needs explicit instruction.

## Verification

- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## Rollback

- Revert the branch commits after `4b40a14` in reverse order if this visual consolidation needs to be backed out.
