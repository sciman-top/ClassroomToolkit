# ClassroomToolkit Photo Touch Flick Inertia Design

- Date: 2026-04-19
- Project: `ClassroomToolkit`
- Scope: PDF/图片全屏场景下的触屏单指甩动、惯性平移、跨页推进与即时刹停
- Status: Draft validated with user, pending user review before implementation planning

## 1. Goal

Deliver a more natural and faster touch-first flick experience for fullscreen PDF/image browsing on classroom all-in-one devices.

The optimized interaction should:

- let teachers use a single finger to drag and flick pages naturally
- support both single-page and cross-page browsing with one coherent feel
- travel farther when the release speed is higher, without feeling exaggerated or random
- stop immediately when the user touches the screen again
- preserve drawing stability and avoid gesture conflicts with ink mode

This is an interaction-quality improvement, not a rewrite of the full photo/PDF feature set.

## 2. Confirmed Direction

The user approved the following design direction:

- primary interaction: `single-finger touch flick`
- secondary interaction: keep mouse drag as compatibility only, not the main tuning target
- zoom strategy: `two-finger pinch remains dedicated to zoom`
- motion target: `甩得远、翻得快`, but still naturally matched to release speed and able to stop precisely
- interruption rule: touching again during inertia must stop movement immediately

## 3. Current State

### 3.1 What already exists

The codebase already has two motion paths:

- a custom pan inertia path used by mouse and stylus pan
- a WPF `Manipulation` path used by touch translation and pinch

Relevant files:

- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.PanInertia.cs`
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Manipulation.cs`
- `src/ClassroomToolkit.App/Paint/PhotoPanInertiaMotionPolicy.cs`
- `src/ClassroomToolkit.App/Paint/PhotoManipulationInertiaPolicy.cs`

### 3.2 Why the current touch feel is limited

The current touch path depends on WPF manipulation inertia deceleration only. That is sufficient for basic glide, but it leaves several teaching-scene issues:

- touch and mouse/stylus do not share the same release-velocity model
- cross-page and single-page feel are not guaranteed to match
- the system-level manipulation inertia is harder to tune toward a strong but still believable classroom flick
- interruption and restart behavior can differ from the custom inertia path

## 4. Constraints

### 4.1 Functional constraints

- No regression in existing PDF/image fullscreen browsing.
- No regression in pinch zoom.
- No regression in ink-mode write behavior.
- No change to persisted file formats or settings semantics unless explicitly required.

### 4.2 Performance constraints

- Interaction must remain responsive on classroom all-in-one devices.
- No heavy synchronous redraw loops during active flick.
- Cross-page updates must avoid reintroducing zoom/pan flicker regressions already fixed in recent work.

### 4.3 Input-safety constraints

- In ink-writing mode, touch movement must not unexpectedly pan the page.
- New touch-down during inertia must cancel inertia before any new gesture state is applied.
- Multi-touch zoom and single-touch flick must not compete for ownership of the same gesture.

## 5. Design Principles

### 5.1 Touch-first realism

The motion should feel like a teacher directly throwing the page with a finger, not like a timer-driven scripted animation.

### 5.2 One motion model

Single-page pan, cross-page pan, and flick continuation should come from one unified inertia core so the feel is predictable.

### 5.3 Strong but bounded

Fast flicks should travel farther and page faster, but all release speed and frame translation must stay bounded to avoid teleport-like jumps.

### 5.4 Immediate interruption

Any new touch contact during inertia must behave like physically grabbing the page and stopping it.

### 5.5 Mode clarity

Single-finger flick belongs to browse/cursor semantics, not brush-writing semantics.

## 6. Recommended Approach

The selected approach is:

1. keep separate event-entry layers for mouse/stylus/touch
2. unify the actual inertia physics and interruption rules into one pan inertia core
3. move touch single-finger pan/flick onto the same release-velocity-based inertia model now used by the custom pan path
4. keep two-finger pinch under manipulation handling, but isolate it from single-finger flick ownership

This is better than either:

- keeping WPF manipulation inertia as the long-term touch solution, because it limits believable high-speed tuning
- forcing mouse/stylus/touch into one raw event pipeline, because their event semantics differ and that would raise regression risk unnecessarily

## 7. Interaction Specification

### 7.1 Gesture ownership

- Single finger:
  - in photo browse/cursor semantics, owns drag and flick
  - in ink-writing semantics, does not own page pan
- Two fingers:
  - own pinch zoom
  - may continue to allow translation during pinch if the current behavior depends on it, but zoom remains the dominant semantic

### 7.2 Flick start rule

Single-finger motion does not immediately become a page throw. It first passes a short translation threshold, then becomes an active pan gesture.

This protects against:

- tap jitter
- accidental touch on large displays
- minor contact noise from classroom all-in-one hardware

### 7.3 Release rule

When the finger leaves the screen:

- collect recent touch movement samples from a short time window
- compute weighted release velocity with stronger weight on more recent samples
- reject inertia if velocity is below threshold
- clamp to a maximum release speed
- start a custom rendering-loop inertia phase

This preserves natural mapping:

- slow release -> short glide or no glide
- medium release -> useful continuation
- strong release -> farther travel and faster cross-page progression

### 7.4 Cross-page rule

Cross-page browsing should not use a separate feel. It should use the same release velocity and deceleration core, with only bounded differences in translation resistance and page-boundary behavior.

Expected result:

- same finger action produces the same basic feel
- cross-page mode does not suddenly become sticky or overly slippery
- flick can push quickly through page seams without looking discontinuous

### 7.5 Interruption rule

When inertia is active and the user touches again:

- stop inertia immediately
- commit the current translation state
- start the next touch interaction from the stopped position

The stop should happen before any new translation accumulation so the interaction feels like grabbing a moving page.

## 8. Architecture Changes

### 8.1 Keep event entry separate

Keep these entry points:

- mouse events
- stylus events
- touch/manipulation events

Reason:

- event timing and ownership differ
- WPF touch/manipulation and mouse capture have different lifecycle semantics
- preserving current entry structure lowers regression risk

### 8.2 Unify motion core

Create or reshape a shared touch-capable pan inertia core responsible for:

- sample collection
- release velocity resolution
- per-frame translation
- deceleration
- duration cap
- frame translation clamp
- interruption and stop rules

The existing custom inertia policy is the right destination for this logic, extended from mouse/stylus-oriented semantics into pointer-source-agnostic semantics.

### 8.3 Separate pointer-source tuning

The shared inertia core may still accept pointer-source-specific tuning presets:

- touch
- mouse
- stylus

The physics engine is shared, but tuning may differ where justified. In this phase, touch tuning is the priority target. Mouse remains compatibility behavior and should not block the touch-first outcome.

## 9. Tuning Direction

### 9.1 Desired touch profile

The touch profile should feel:

- stronger than the current default manipulation inertia
- more willing to continue across visible page seams
- immediately interruptible
- still bounded and non-chaotic

### 9.2 Likely tuning changes

Expected tuning direction for touch:

- lower minimum release threshold than mouse, so finger flick is easier to trigger
- higher max release speed than the current conservative baseline, but still clamped
- slightly lower deceleration in touch flick mode so strong throws travel farther
- clear duration cap so inertia never drifts too long
- stronger boundary handling so hitting hard limits feels damped, not broken

Final numbers should be chosen from device testing, not guessed in the spec.

## 10. Rendering and Smoothness Strategy

Touch realism depends on rendering behavior as much as on physics.

During active pan/flick:

- prefer the existing low-quality interaction scaling mode
- avoid forcing expensive cross-page full refresh on every movement
- keep visible neighbor transforms synchronized immediately

After motion settles:

- restore high-quality scaling
- run deferred cross-page reconciliation if needed

This aligns with the recent zoom/pan smoothness fixes already recorded in project evidence and avoids undoing them.

## 11. Error Handling and Safety

- If touch gesture ownership becomes ambiguous, prefer stopping inertia and falling back to stable non-inertial behavior rather than continuing with a wrong gesture owner.
- If rendering timing is invalid or elapsed time is abnormal, clamp frame duration and stop motion when necessary.
- If photo mode exits, page source changes, or zoom mode takes ownership, stop inertia immediately.
- If cross-page bounds cannot be resolved, degrade to bounded single-surface translation rather than leaving movement unconstrained.

## 12. Testing Strategy

### 12.1 Unit and policy tests

Add or update tests for:

- touch release velocity estimation
- minimum/maximum release speed behavior
- deceleration and duration stop conditions
- interruption by new touch-down
- cross-page and single-page shared-core behavior

### 12.2 Contract and regression tests

Protect:

- manipulation lifecycle ownership
- composition-rendering-based inertia loop
- pinch zoom behavior
- existing photo render-quality and deferred-refresh coordination

### 12.3 Manual acceptance focus

Manual checks should cover:

- single-page slow drag release
- single-page strong finger flick
- cross-page strong finger flick across multiple seams
- touch again during inertia and confirm immediate stop
- pinch zoom after flick and flick after pinch
- ink mode vs browse mode interaction separation
- 4K classroom all-in-one touch hardware behavior

## 13. Non-Goals

- Rebuilding mouse drag around a new product experience
- Changing keyboard navigation semantics
- Reworking PDF/image loading architecture
- Introducing additional user-facing complexity beyond justified tuning or profile adjustments

## 14. Recommendation

Proceed with:

- `single-finger flick as the primary browse interaction`
- `two-finger pinch as the zoom interaction`
- `shared inertia core for single-page and cross-page motion`
- `immediate stop on new touch-down`
- `touch-first tuning optimized for longer, faster, but still believable throws`

This is the best balance of classroom speed, natural feel, maintainability, and regression control for the current codebase.
