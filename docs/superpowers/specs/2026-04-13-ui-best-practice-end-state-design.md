# ClassroomToolkit UI Best Practice End State Design

- Date: 2026-04-13
- Project: `ClassroomToolkit`
- Scope: WPF UI visual unification and core-window polish
- Status: Draft validated with user, pending user review before implementation planning

## 1. Goal

Deliver a UI "best practice end state" for ClassroomToolkit that is:

- visually unified across all windows and dialogs
- compact and efficient without becoming cramped
- clearer in hierarchy, labels, and action emphasis
- appropriate for classroom use with low distraction
- safe for runtime performance on floating windows, overlays, and content-heavy views

This is a visual and interaction-surface refinement effort, not a product redesign or behavior rewrite.

## 2. Confirmed Direction

The user approved the following design direction:

- visual direction: `精致现代化`
- theme boundary: keep the current dark classroom-tool baseline
- implementation path: `theme unification + core-window polish`
- visual companion: not enabled

## 3. Constraints

### 3.1 Functional constraints

- No semantic regression in existing classroom workflows.
- Do not change file format or user-facing compatibility for:
  - `students.xlsx`
  - `student_photos/`
  - `settings.ini`
- Do not weaken visibility or usability of high-frequency classroom actions.

### 3.2 Performance constraints

- No heavy continuous animations.
- No expensive visual effects on frequently redrawn overlays.
- No style changes that break virtualization in list/tree/grid views.
- Prefer shared resources, frozen brushes/geometries, and lightweight templates.
- Fullscreen and floating windows must remain responsive during drag, zoom, page switch, and close.

### 3.3 Delivery constraints

- Reuse and extend the existing theme-resource architecture instead of replacing it wholesale.
- Keep code changes concentrated in XAML resources and window markup where possible.
- Only adjust code-behind or view-model display text when necessary for accurate semantics or state presentation.

## 4. Design Principles

### 4.1 Classroom-first polish

The UI should feel refined and modern, but the primary value remains classroom usability. Content and actions must remain more prominent than decoration.

### 4.2 Dense but comfortable

Controls, toolbars, and dialogs should become more compact and consistent, while retaining safe click targets and clear grouping.

### 4.3 Unified visual grammar

All windows should read as one product family. Different shells may serve different roles, but they must come from one coherent token and component system.

### 4.4 Meaning before style

Shorter labels, stronger hierarchy, and cleaner grouping take priority over adding more effects. If a label or icon meaning is unclear, semantics must be verified in code before it is changed.

### 4.5 Content-first overlays

In PDF, image, fullscreen, roll-call, and timer scenes, controls should visually recede so the teaching content remains dominant.

## 5. Recommended Approach

The selected implementation approach is:

1. unify global theme tokens and reusable control styles
2. polish high-frequency and high-visibility windows
3. normalize the remaining dialogs to the same shell language

This balances consistency, impact, maintainability, and runtime safety better than either token-only cleanup or full per-window redesign.

## 6. Scope

### 6.1 Global theme unification layer

Primary targets:

- `src/ClassroomToolkit.App/App.xaml`
- `src/ClassroomToolkit.App/Assets/Styles/Colors.xaml`
- `src/ClassroomToolkit.App/Assets/Styles/WidgetStyles.xaml`
- `src/ClassroomToolkit.App/Assets/Styles/Icons.xaml`

This layer will define and normalize:

- semantic color roles
- density and spacing tokens
- typography tiers
- icon sizing and button sizing
- shell styles for work, management, dialog, and fullscreen contexts
- shared styles for common controls

### 6.2 Core-window polish layer

Priority windows:

- `src/ClassroomToolkit.App/MainWindow.xaml`
- `src/ClassroomToolkit.App/RollCallWindow.xaml`
- `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml`
- `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml`
- `src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml`
- `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml`
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml`

Secondary windows/dialogs to normalize after the core pass:

- `src/ClassroomToolkit.App/AboutDialog.xaml`
- `src/ClassroomToolkit.App/AutoExitDialog.xaml`
- `src/ClassroomToolkit.App/ClassSelectDialog.xaml`
- `src/ClassroomToolkit.App/RemoteKeyDialog.xaml`
- `src/ClassroomToolkit.App/RollCallSettingsDialog.xaml`
- `src/ClassroomToolkit.App/StudentListDialog.xaml`
- `src/ClassroomToolkit.App/TimerSetDialog.xaml`
- `src/ClassroomToolkit.App/Diagnostics/DiagnosticsDialog.xaml`
- `src/ClassroomToolkit.App/Diagnostics/StartupCompatibilityWarningDialog.xaml`
- `src/ClassroomToolkit.App/Ink/InkSettingsDialog.xaml`
- `src/ClassroomToolkit.App/Paint/BoardColorDialog.xaml`
- `src/ClassroomToolkit.App/Paint/QuickColorPaletteWindow.xaml`
- `src/ClassroomToolkit.App/Paint/RegionSelectionOverlayWindow.xaml`
- `src/ClassroomToolkit.App/Photos/RollCallGroupOverlayWindow.xaml`
- `src/ClassroomToolkit.App/LauncherBubbleWindow.xaml`

## 7. Visual System Specification

### 7.1 Color system

The current dark baseline remains, but color usage becomes stricter and more semantic.

Rules:

- background layers must be calmer and easier to distinguish
- border contrast must be more consistent
- primary accent remains blue-led and restrained
- teaching/success green is reserved for teaching or positive states
- warning and danger colors appear only for explicit risk actions
- non-essential multi-color emphasis should be reduced

Expected outcome:

- fewer competing accents on a single screen
- stronger action priority
- cleaner scan path in dense settings and management windows

### 7.2 Density system

A unified sizing scale will be enforced across:

- title bar heights
- icon button sizes
- action button heights
- input heights
- card padding
- inter-group spacing
- divider spacing

Outcome:

- floating tools feel tighter and sharper
- dialogs feel more efficient
- management screens stop oscillating between sparse and crowded sections

### 7.3 Typography system

Typography will be reduced to a clearer hierarchy:

- window title
- section title
- control label
- body text
- helper text

Rules:

- helper text must be visibly secondary
- labels should stay short and direct
- large-display scenes such as timer and roll call may use stronger display treatment
- non-display windows should avoid oversized text unless it marks a true section boundary

### 7.4 Icon and button system

Icons and button shells will be unified by:

- common icon size bands
- consistent visual weight
- normalized internal padding
- stricter separation of primary, secondary, neutral, and danger actions

Where icon-only controls are ambiguous, tooltips or shorter visible labels may be improved after semantic verification.

### 7.5 Surface and shell system

Four shell families will remain, but they will be normalized:

- work shell: compact, action-oriented floating windows
- management shell: denser information and navigation surfaces
- dialog shell: focused task completion with clear footer actions
- fullscreen shell: minimal chrome, content-first overlays

Each shell family must still read as the same product.

## 8. Window-Specific Design Intent

### 8.1 MainWindow

Intent:

- make the main entry window feel like a compact classroom command hub
- strengthen the two primary hero actions
- reduce visual noise in the bottom utility bar

Planned outcomes:

- clearer primary-vs-secondary action separation
- more disciplined title/header composition
- tighter bottom utility cluster

### 8.2 RollCallWindow

Intent:

- unify roll-call and timer modes under one shell language
- improve the relationship between title bar, central content, and bottom actions
- keep the student name or timer as the visual anchor

Planned outcomes:

- stronger center-stage content emphasis
- less visual mismatch between roll-call and timer mode
- cleaner action grouping and better spacing discipline

### 8.3 PaintToolbarWindow

Intent:

- make the toolbar genuinely compact, premium, and easy to parse at a glance
- reduce the current feeling of many small groups being placed side by side without enough hierarchy

Planned outcomes:

- better grouping rhythm
- more consistent icon emphasis
- clearer active, neutral, and destructive states
- preserved one-hand classroom usability

### 8.4 PaintSettingsDialog

Intent:

- improve the highest-density settings surface in the app
- make grouping, advanced sections, and helper text easier to scan
- shorten labels without losing meaning

Planned outcomes:

- cleaner tab interiors
- more consistent field-label widths and spacing
- stronger distinction between standard and advanced options
- reduced reading fatigue

### 8.5 ImageManagerWindow

Intent:

- make the management window feel more deliberate and less patchwork
- unify title area, folder navigation, view controls, and content pane
- preserve performance and virtualization

Planned outcomes:

- calmer left/right pane relationship
- tighter, more professional toolbar and address-bar composition
- improved thumbnail/list visual consistency

### 8.6 PhotoOverlayWindow and other fullscreen/content-first windows

Intent:

- keep the content visually dominant
- make controls present but non-intrusive

Planned outcomes:

- lower visual weight for close and hint controls
- stronger readability for badges when shown
- less "floating UI clutter" over teaching content

### 8.7 Remaining dialogs

Intent:

- standardize them into one dialog family
- normalize title bars, footer buttons, spacing, and helper-text treatment

Planned outcomes:

- easier maintenance
- less stylistic drift across smaller windows

## 9. Copy and Semantics Rules

Copy refinement is in scope, but only under these rules:

- shorten text when the same meaning can be preserved
- remove repetition and over-explanation
- prefer direct action labels and short helper text
- verify semantics against code-behind, bindings, and tests before renaming unclear options

Examples of allowed copy changes:

- compressing long helper sentences
- making button labels shorter and clearer
- making section headers more direct

Examples of prohibited copy changes:

- changing a feature's meaning without semantic verification
- inventing new terminology not supported by current behavior
- hiding risk behind softer wording for destructive actions

## 10. Non-Goals

This design does not include:

- changing business rules
- changing file/storage formats
- rewriting navigation architecture
- redesigning workflows unrelated to visual presentation
- adding animation-heavy motion design
- introducing a brand-new theme family unrelated to the current dark baseline

## 11. Risks and Mitigations

### 11.1 Risk: style unification breaks local visual assumptions

Mitigation:

- preserve shell families
- update high-frequency windows together with shared tokens
- validate contract/style tests after implementation

### 11.2 Risk: over-compaction harms usability

Mitigation:

- retain adequate hit targets
- separate visual compactness from interactive hit area
- prioritize classroom speed over visual minimalism

### 11.3 Risk: performance regressions from visual effects

Mitigation:

- keep effects sparse
- avoid repeated heavy shadows on dynamic surfaces
- prefer static resources and lightweight rendering paths

### 11.4 Risk: copy tightening changes meaning

Mitigation:

- verify ambiguous labels in code before edits
- keep destructive and compatibility-related wording explicit

## 12. Validation Expectations For Implementation

Implementation must later validate:

- build passes
- full test suite passes
- contract/invariant subset passes
- hotspot budget script passes

Additional visual verification should cover:

- main window primary actions
- roll-call and timer readability
- toolbar density and hit targets
- settings dialog scanability
- image manager list/thumbnail views
- fullscreen overlay readability and restraint

## 13. Implementation Boundary For Next Phase

The next phase should produce an implementation plan that:

- sequences shared theme work before per-window polish
- groups windows by shell family and risk
- keeps scope controlled
- includes verification checkpoints after shared-style changes

Implementation should prefer incremental landing rather than one giant UI rewrite.
