# ClassroomToolkit Touch-First Low-Occlusion Controls Design

- Date: 2026-04-20
- Project: `ClassroomToolkit`
- Scope: 触屏一体机场景下的低遮挡、低认知负担、可发现的控件与设置入口设计
- Status: Draft validated with user, pending user review before implementation planning

## 1. Goal

Deliver a touch-first interaction model that stays compact on screen while removing hidden, mouse-centric primary paths.

The optimized interaction should:

- keep the toolbar and launcher visually compact enough for teaching content display
- keep primary actions discoverable without relying on mouse-only long press, hover, or double-click
- reduce accidental touches and repeated precision tapping on large classroom touch displays
- make frequent tool adjustments reachable in one short path
- preserve a clean visual surface by avoiding permanent expansion of low-frequency controls

This is an interaction architecture adjustment, not a request to add visible buttons everywhere.

## 2. Confirmed Direction

The user approved the following direction:

- the UI must remain concise and low-occlusion
- launcher bubble should remain visually small and refined
- explicit access does not mean multiplying always-visible buttons
- the preferred primary pattern is `tap selected tool again to expand its settings`
- a unified global `...` entry is allowed only for low-frequency, cross-tool actions
- hidden gestures may remain as accelerators, but must not remain the only path for core touch flows

## 3. Current Problem Framing

### 3.1 Why adding more permanent buttons is the wrong default

A classroom toolbar competes directly with PPT, PDF, images, and whiteboard content. If every configurable tool gets its own always-visible settings or overflow button, three regressions appear immediately:

- the toolbar consumes more horizontal width and more visual attention
- fine-grained buttons become either too many or too small
- users still need to learn which extra button belongs to which tool

So the target is not `more buttons`. The target is `fewer visible controls with clearer ownership`.

### 3.2 Why the current hidden-path model is still insufficient

The current codebase relies too heavily on long press, double-click, right-click, tooltip, or mode-specific hidden behavior in places that affect touch-first classroom flow.

That keeps the screen visually clean, but it shifts cost into:

- discoverability
- training burden
- missed touches and retries
- fragile classroom operation under time pressure

The design must therefore rebalance from `hidden by default` to `compact but still discoverable`.

## 4. Constraints

### 4.1 Product constraints

- No redesign that materially increases content occlusion during teaching.
- No assumption that keyboard or mouse is available.
- No requirement for the teacher to remember hover-only hints.
- No primary flow may depend exclusively on mouse-only event semantics.

### 4.2 Interaction constraints

- Visual size and hit size may differ: controls may look compact while keeping a larger touch target.
- Expanded settings must auto-collapse cleanly to restore screen space.
- Low-frequency settings should not stay pinned unless the user explicitly opens a broader settings surface.
- One-hand use on large displays should remain practical.

### 4.3 Implementation constraints

- Existing feature semantics should be preserved where possible; this is a path redesign, not a feature rewrite.
- Changes should be staged from shared touch primitives to high-frequency surfaces, then to low-frequency dialogs.
- Touch, mouse, and stylus may have different accelerators, but the main path must be valid for touch.

## 5. Design Principles

### 5.1 Compact by default, explicit on demand

The resting UI stays small. Additional controls appear only when the user expresses intent by interacting with the currently active tool or opening a global overflow.

### 5.2 Ownership must be obvious

If a setting belongs to one tool, it should open from that tool. If it belongs to the whole system, it should live in one shared overflow or settings surface.

### 5.3 Primary paths must be tap-based

The first successful path for a touch user should be reachable through tap, repeat tap, swipe, or direct visible action. Long press, right click, and double-click become optional accelerators only.

### 5.4 Visual compactness cannot sacrifice touch reliability

The project should separate `visual footprint` from `interactive footprint`. Small-looking controls are acceptable; tiny hit targets are not.

### 5.5 High-frequency beats perfect minimalism

When a hidden design saves a few pixels but repeatedly slows classroom operation, the hidden design is worse. Teaching flow takes priority over theoretical minimalism.

## 6. Options Considered

### 6.1 Option A: Add a visible settings button to each configurable tool

Pros:

- strongest discoverability
- simple mental model

Cons:

- highest occlusion cost
- toolbar becomes visually noisy
- scales poorly as tools gain options

This option is rejected as the default model.

### 6.2 Option B: One global `...` overflow for everything

Pros:

- visually very compact
- consistent single entry point

Cons:

- adds one more navigation step for frequent tool adjustments
- weak ownership: users must search inside a shared panel to find tool-specific settings
- slower during teaching

This option is acceptable only for low-frequency global actions.

### 6.3 Option C: Tap selected tool again to open tool-local settings, plus one optional global `...`

Pros:

- preserves compact resting UI
- keeps ownership obvious
- minimizes step count for common adjustments
- avoids multiplying permanent buttons

Cons:

- requires a small discoverability cue for the selected tool
- needs careful expand/collapse behavior to avoid accidental toggling

This is the recommended option.

## 7. Recommended Interaction Model

### 7.1 Toolbar rule

For toolbar tools, the default interaction contract becomes:

- tap an unselected tool: activate that tool
- tap the already selected tool: open that tool's local settings panel
- tap outside, switch tools, or complete the action: close the local settings panel

This keeps the main bar short while giving touch users a repeatable, visible path.

### 7.2 Global overflow rule

Keep at most one lightweight global `...` entry for actions that are:

- cross-tool
- low-frequency
- not needed in the immediate teaching loop

Examples include advanced preferences, layout options, help, or rarely used toggles.

The global `...` entry is optional per surface. It should exist only where low-frequency global actions are actually needed; it is not a mandatory ornament for every toolbar or floating control.

### 7.3 Launcher bubble rule

The launcher bubble remains visually small, but its interactive region must be larger than its visible ornament.

The bubble should support:

- easy single tap to restore
- stable touch drag with edge snap
- no requirement for precise fingertip targeting on the visible circle alone

### 7.4 Hidden gesture rule

Long press, double-click, and right-click may remain only as shortcuts for experienced users. Any action needed by a touch-first teacher must have a visible tap-based route.

### 7.5 Eligibility rule

Only controls with meaningful local settings should support `tap selected item again to expand`.

Controls without local settings should keep a simpler contract:

- tap to activate
- no fake expandable state
- no decorative affordance implying hidden options

## 8. Surface-Specific Specification

### 8.1 Paint toolbar

- Do not add a permanent settings button beside every tool.
- Selected tool exposes a compact attached panel on second tap.
- The attached panel should contain only that tool's highest-frequency settings.
- Advanced or rare settings move to the single global overflow or full settings dialog.
- Only tools that actually expose local settings should signal expandability.
- The selected state for expandable tools needs a clear affordance that suggests expandability, such as a subtle indicator or state styling.

### 8.2 Quick color and brush controls

- Color and stroke settings belong to the active drawing tool, not to a detached hidden path.
- Opening them should not require long press.
- The first layer should stay compact: common swatches, thickness presets, and maybe one advanced entry.
- Dense micro-controls should be avoided inside the first-level popover.

### 8.3 Photo/PDF browsing overlay

- Frequent browse actions must stay directly reachable through touch-friendly tap or gesture paths.
- If a browsing mode has tool-local options, those options should follow the same `selected tool -> second tap opens panel` rule where applicable.
- Mouse-right-click-only paths should be demoted from primary status.

### 8.4 Main window tiles

- Large primary launch tiles remain simple single-tap targets.
- If a tile needs configuration, its low-frequency options should open from a single visible secondary affordance or from a shared overflow, not from mouse-only long press.
- Avoid placing multiple small secondary icons directly on each tile.

### 8.5 Dialogs and settings pages

- Low-frequency dialogs can be slightly denser than the toolbar, but they still must not rely on hover or double-click.
- Repeated increment/decrement tasks should prefer touch-sized steppers or presets.
- Text inputs should avoid auto-focusing on open unless input is clearly the primary task.

## 9. Touch Metrics and Interaction Baseline

### 9.1 Size baseline

Recommended default baseline:

- minimum touch hit target for common controls: `44-48 DIP`
- primary/high-frequency controls: `48-56 DIP`
- launcher visible ornament may be smaller, but hit region should stay at least `56-64 DIP`
- attached popover controls should avoid sub-40 DIP primary targets

### 9.2 Trigger baseline

- tap should win over hover-based explanation
- second-tap expansion must have a stable debounce/threshold so it does not misfire during drag intent
- drag handles and scroll affordances need touch-specific thresholds, not mouse-only thresholds

### 9.3 Dismiss baseline

Expanded panels should close on:

- outside tap
- switching to another tool
- explicit close where needed
- entering a mode that makes the panel irrelevant

Dismissal should be predictable and should not unexpectedly commit destructive actions.

## 10. Rollout Strategy

### 10.1 Phase 1: Shared primitives

- define touch-first size tokens and hit-target rules
- replace mouse-only core gesture dependencies for settings exposure
- establish a reusable local-settings popover pattern

### 10.2 Phase 2: High-frequency surfaces

- paint toolbar
- launcher bubble
- photo/PDF browsing controls
- main entry tiles where touch discovery is currently hidden

### 10.3 Phase 3: Low-frequency dialogs

- timer, roll call, class selection, and settings dialogs
- unify touch scrolling, steppers, and focus behavior

## 11. Acceptance Criteria

The design is considered correctly implemented when:

- a touch user can reach common tool settings without mouse-only long press, hover, or double-click
- the toolbar remains visually compact in resting state
- the number of permanent visible buttons on the toolbar does not materially increase
- launcher bubble remains visually small but becomes easier to hit and drag by touch
- high-frequency adjustments take no more than one extra tap from the active tool
- low-frequency global actions are consolidated instead of being duplicated per tool

## 12. Non-Goals

This design does not require:

- turning every hidden shortcut into a permanent on-screen control
- redesigning the full application visual style
- rewriting low-frequency settings dialogs before the high-frequency teaching surfaces are fixed
- removing mouse or stylus accelerators that remain compatible with the touch-first path
