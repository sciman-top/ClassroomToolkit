# 2026-04-28 UI best-practice refresh

## Boundary

- current_landing: `src/ClassroomToolkit.App` WPF resource dictionaries and XAML windows.
- target_destination: visual best-practice end state for the classroom launcher, roll-call/timer, settings dialogs, PDF/image management, toolbar, about, diagnostics, and photo surfaces.
- risk_level: medium visual-surface change; no business logic, storage format, Interop, dependency, or schema change.

## Basis

- User requested a comprehensive visual polish pass across all windows while keeping controls compact, copy clear, and runtime performance unaffected.
- Project rule order: `build -> test -> contract/invariant -> hotspot`.
- Existing UI contract tests lock touch target sizes, short copy, photo-overlay close behavior, semantic color usage, and XAML style keys.

## Changes

- Neutralized the dark classroom shell palette from green-heavy surfaces to graphite surfaces while preserving teal, amber, and violet teaching accents.
- Added shared `Style_SurfaceSectionBorder` and `Style_SurfaceSectionBorder_Danger` for summary/info panels, then applied them to About and diagnostics surfaces.
- Removed hover-state visual changes from non-interactive setting cards so panels do not imply clickability.
- Kept disabled active icon buttons visually subdued.
- Changed the main launcher primary label from `批注` to `画笔` for clearer first-scan semantics.
- Changed About app icon glyph to use the semantic dark text brush on the primary badge for better contrast.
- Changed PDF/image manager toolbar container from `StackPanel` to `WrapPanel` so dense controls can wrap instead of clipping at narrower widths.
- Reduced photo-overlay name badge max width and font size to avoid oversized overlays on classroom screens.
- Removed the roll-call loading overlay's always-loaded rotating storyboard; the loading indicator is now static to avoid hidden animation work.
- Kept tested touch-first copy and handler contracts intact after restoring string-level test expectations.

## Verification

1. `dotnet build ClassroomToolkit.sln -c Debug`
   - initial result: blocked by host process environment, `Value cannot be null. (Parameter 'path1')`.
   - action: dot-sourced `scripts/env/Initialize-WindowsProcessEnvironment.ps1`.

2. `. .\scripts\env\Initialize-WindowsProcessEnvironment.ps1; dotnet build ClassroomToolkit.sln -c Debug`
   - result: pass.
   - key_output: `0 warning`, `0 error`.

3. `. .\scripts\env\Initialize-WindowsProcessEnvironment.ps1; dotnet test tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj -c Debug`
   - first self-repair result: failed on two string-level UI contracts; restored the locked toolbar tooltip copy and direct `Brush_Surface_Secondary` reference in `AboutDialog.xaml`.
   - final result: pass.
   - key_output: `3471 passed`, `0 failed`.

4. `. .\scripts\env\Initialize-WindowsProcessEnvironment.ps1; dotnet test tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - result: pass.
   - key_output: `28 passed`, `0 failed`.

5. `. .\scripts\env\Initialize-WindowsProcessEnvironment.ps1; powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality\check-hotspot-line-budgets.ps1`
   - result: pass.
   - key_output: `[hotspot] PASS - all .cs files within line budget (max=1200)`.

6. `rg -n '#[0-9A-Fa-f]{6,8}' <core-window-xaml-files>`
   - result: no matches.
   - interpretation: core windows still use semantic color resources.

7. `git diff --check`
   - result: pass.
   - key_output: only line-ending normalization warnings; no whitespace errors.

## Hotspot Review

- touched surfaces: `Colors.xaml`, `WidgetStyles.xaml`, `MainWindow.xaml`, `RollCallWindow.xaml`, `ImageManagerWindow.xaml`, `PhotoOverlayWindow.xaml`, `PaintToolbarWindow.xaml`, `AboutDialog.xaml`, `DiagnosticsDialog.xaml`, `StartupCompatibilityWarningDialog.xaml`.
- classroom usability: no touch target tokens were reduced; toolbar local compact visual size remains backed by existing tests.
- Interop/windowing: no z-order, topmost, owner, hook, COM, WPS, or UIAutomation code changed.
- performance: no new animation, blur, package, or runtime dependency added; one hidden repeated storyboard was removed; existing virtualization in image lists was preserved.
- compatibility: no changes to `students.xlsx`, `student_photos/`, `settings.ini`, settings schema, or persistence behavior.

## Rollback

If the change is still uncommitted, run:

```powershell
git restore -- src/ClassroomToolkit.App/AboutDialog.xaml src/ClassroomToolkit.App/Assets/Styles/Colors.xaml src/ClassroomToolkit.App/Assets/Styles/WidgetStyles.xaml src/ClassroomToolkit.App/Diagnostics/DiagnosticsDialog.xaml src/ClassroomToolkit.App/Diagnostics/StartupCompatibilityWarningDialog.xaml src/ClassroomToolkit.App/MainWindow.xaml src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml src/ClassroomToolkit.App/RollCallWindow.xaml
Remove-Item -LiteralPath docs/change-evidence/20260428-ui-best-practice-refresh.md
```

If the change has been committed, restore the same paths from the parent commit or revert the commit.

Then rerun `build -> test -> contract/invariant -> hotspot`.
