# Photo Touch Flick Inertia Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade fullscreen PDF/image browsing so classroom touchscreens get a fast, natural single-finger flick experience with shared single-page/cross-page inertia, immediate stop on new touch-down, and no regression in pinch zoom or ink mode.

**Architecture:** Keep mouse, stylus, touch, and manipulation as separate event-entry layers, but move touch flick motion onto the same custom release-velocity inertia core already used by the current pan pipeline. Single-finger touch owns drag/flick, two-finger manipulation remains the zoom owner, and promoted touch-stylus input is explicitly filtered so one physical finger does not drive two code paths.

**Tech Stack:** .NET 10, WPF, xUnit, FluentAssertions, existing `PaintOverlayWindow` partial-class structure

---

## File Map

### New files

- `src/ClassroomToolkit.App/Paint/PhotoTouchInteractionPolicy.cs`
  - Defines touch ownership rules: when single-touch pan is allowed, when manipulation should own the gesture, and when promoted touch stylus input must be ignored.
- `src/ClassroomToolkit.App/Paint/PhotoPanReleaseTuning.cs`
  - Defines pointer-kind-aware release tuning primitives used by the shared inertia core.
- `src/ClassroomToolkit.App/Paint/PhotoPanReleaseTuningPolicy.cs`
  - Maps the existing profile-based inertia tuning to mouse/stylus/touch release tuning values.
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Touch.cs`
  - Handles `TouchDown/Move/Up/LostTouchCapture` for single-finger pan/flick ownership.
- `tests/ClassroomToolkit.Tests/PhotoTouchInteractionPolicyTests.cs`
  - Covers touch ownership and promoted-touch stylus filtering.
- `tests/ClassroomToolkit.Tests/PhotoPanReleaseTuningPolicyTests.cs`
  - Covers touch-vs-mouse release tuning differences.
- `tests/ClassroomToolkit.Tests/PhotoTouchInputContractTests.cs`
  - Guards touch event registration, promoted-touch stylus suppression, and shared inertia entry points.
- `docs/change-evidence/20260419-photo-touch-flick-inertia.md`
  - Stores the final evidence, gate output, hotspot review, and rollback notes for the implementation.

### Modified files

- `src/ClassroomToolkit.App/Paint/PhotoInputAlignmentPolicy.cs`
  - Extend manipulation routing to consider active touch count and consume single-touch manipulation while touch-pan owns the gesture.
- `src/ClassroomToolkit.App/Paint/PhotoManipulationAdmissionPolicy.cs`
  - Pass active touch count into the routing policy.
- `src/ClassroomToolkit.App/Paint/PhotoPanInertiaMotionPolicy.cs`
  - Accept pointer-specific release tuning while preserving the existing rendering-loop inertia core.
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.State.cs`
  - Add active touch IDs, current touch pan device, and active pan pointer kind runtime state.
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs`
  - Register touch handlers on `OverlayRoot`.
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Lifecycle.cs`
  - Unregister touch handlers and touch capture cleanup.
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Stylus.cs`
  - Ignore promoted touch stylus input so finger contact does not double-trigger stylus pan.
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Manipulation.cs`
  - Restrict manipulation ownership to multi-touch zoom and stop conflicting single-touch translation ownership.
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.PanInertia.cs`
  - Track pan pointer kind and resolve touch release tuning when the flick originated from touch.
- `tests/ClassroomToolkit.Tests/PhotoInputAlignmentPolicyTests.cs`
  - Update manipulation routing expectations for active touch count.
- `tests/ClassroomToolkit.Tests/PhotoPanInertiaMotionPolicyTests.cs`
  - Add touch-tuning-specific motion tests.
- `tests/ClassroomToolkit.Tests/PhotoManipulationInertiaPolicyTests.cs`
  - Keep multi-touch zoom deceleration coverage focused on manipulation-only zoom semantics.

## Task 1: Separate Touch Ownership From Manipulation Routing

**Files:**
- Create: `src/ClassroomToolkit.App/Paint/PhotoTouchInteractionPolicy.cs`
- Modify: `src/ClassroomToolkit.App/Paint/PhotoInputAlignmentPolicy.cs`
- Modify: `src/ClassroomToolkit.App/Paint/PhotoManipulationAdmissionPolicy.cs`
- Create: `tests/ClassroomToolkit.Tests/PhotoTouchInteractionPolicyTests.cs`
- Modify: `tests/ClassroomToolkit.Tests/PhotoInputAlignmentPolicyTests.cs`

- [ ] **Step 1: Write the failing touch-ownership tests**

```csharp
using System.Windows.Input;
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoTouchInteractionPolicyTests
{
    [Theory]
    [InlineData(true, false, PaintToolMode.Cursor, false, 1, true)]
    [InlineData(true, false, PaintToolMode.Cursor, false, 2, false)]
    [InlineData(true, false, PaintToolMode.Brush, false, 1, false)]
    [InlineData(true, false, PaintToolMode.Cursor, true, 1, false)]
    [InlineData(false, false, PaintToolMode.Cursor, false, 1, false)]
    public void ShouldUseSingleTouchPan_ShouldMatchExpected(
        bool photoModeActive,
        bool boardActive,
        PaintToolMode mode,
        bool inkOperationActive,
        int activeTouchCount,
        bool expected)
    {
        PhotoTouchInteractionPolicy.ShouldUseSingleTouchPan(
            photoModeActive,
            boardActive,
            mode,
            inkOperationActive,
            activeTouchCount).Should().Be(expected);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void ShouldUseManipulationZoom_ShouldRequireTwoTouches(
        int activeTouchCount,
        bool expected)
    {
        PhotoTouchInteractionPolicy.ShouldUseManipulationZoom(activeTouchCount).Should().Be(expected);
    }

    [Theory]
    [InlineData(TabletDeviceType.Touch, true)]
    [InlineData(TabletDeviceType.Stylus, false)]
    public void ShouldIgnorePromotedTouchStylus_ShouldMatchExpected(
        TabletDeviceType tabletDeviceType,
        bool expected)
    {
        PhotoTouchInteractionPolicy.ShouldIgnorePromotedTouchStylus(tabletDeviceType).Should().Be(expected);
    }
}
```

```csharp
[Theory]
[InlineData(true, false, PaintToolMode.Cursor, false, false, 1, 1)]
[InlineData(true, false, PaintToolMode.Cursor, false, false, 2, 2)]
[InlineData(true, false, PaintToolMode.Cursor, false, true, 2, 1)]
public void PhotoManipulationRoutingPolicy_ShouldHonorActiveTouchCount(
    bool photoModeActive,
    bool boardActive,
    PaintToolMode mode,
    bool inkOperationActive,
    bool photoPanning,
    int activeTouchCount,
    int expected)
{
    var decision = PhotoManipulationRoutingPolicy.Resolve(
        photoModeActive,
        boardActive,
        mode,
        inkOperationActive,
        photoPanning,
        activeTouchCount);

    ((int)decision).Should().Be(expected);
}
```

- [ ] **Step 2: Run the targeted tests to verify they fail**

Run:

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoTouchInteractionPolicyTests|FullyQualifiedName~PhotoInputAlignmentPolicyTests"
```

Expected:

- FAIL because `PhotoTouchInteractionPolicy` does not exist yet
- FAIL because `PhotoManipulationRoutingPolicy.Resolve(...)` does not accept `activeTouchCount`

- [ ] **Step 3: Add the touch-ownership policy and manipulation routing gate**

```csharp
using System.Windows.Input;

namespace ClassroomToolkit.App.Paint;

internal static class PhotoTouchInteractionPolicy
{
    internal static bool ShouldUseSingleTouchPan(
        bool photoModeActive,
        bool boardActive,
        PaintToolMode mode,
        bool inkOperationActive,
        int activeTouchCount)
    {
        return photoModeActive
            && !boardActive
            && mode == PaintToolMode.Cursor
            && !inkOperationActive
            && activeTouchCount == 1;
    }

    internal static bool ShouldUseManipulationZoom(int activeTouchCount)
    {
        return activeTouchCount >= 2;
    }

    internal static bool ShouldIgnorePromotedTouchStylus(TabletDeviceType tabletDeviceType)
    {
        return tabletDeviceType == TabletDeviceType.Touch;
    }
}
```

```csharp
internal static class PhotoManipulationRoutingPolicy
{
    internal static PhotoManipulationRoutingDecision Resolve(
        bool photoModeActive,
        bool boardActive,
        PaintToolMode mode,
        bool inkOperationActive,
        bool photoPanning,
        int activeTouchCount)
    {
        if (boardActive)
        {
            return PhotoManipulationRoutingDecision.Consume;
        }
        if (!photoModeActive)
        {
            return PhotoManipulationRoutingDecision.Ignore;
        }
        if (mode != PaintToolMode.Cursor || inkOperationActive || photoPanning)
        {
            return PhotoManipulationRoutingDecision.Consume;
        }
        return PhotoTouchInteractionPolicy.ShouldUseManipulationZoom(activeTouchCount)
            ? PhotoManipulationRoutingDecision.Handle
            : PhotoManipulationRoutingDecision.Consume;
    }
}
```

```csharp
internal static class PhotoManipulationAdmissionPolicy
{
    internal static PhotoManipulationEventHandlingPlan Resolve(
        bool photoModeActive,
        bool boardActive,
        PaintToolMode mode,
        bool inkOperationActive,
        bool photoPanning,
        int activeTouchCount)
    {
        var decision = PhotoManipulationRoutingPolicy.Resolve(
            photoModeActive,
            boardActive,
            mode,
            inkOperationActive,
            photoPanning,
            activeTouchCount);
        return PhotoManipulationEventHandlingPolicy.Resolve(decision);
    }
}
```

- [ ] **Step 4: Run the targeted tests to verify they pass**

Run:

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoTouchInteractionPolicyTests|FullyQualifiedName~PhotoInputAlignmentPolicyTests"
```

Expected:

- PASS for the new touch ownership tests
- PASS for the updated manipulation routing expectations

- [ ] **Step 5: Commit**

```powershell
git add src/ClassroomToolkit.App/Paint/PhotoTouchInteractionPolicy.cs src/ClassroomToolkit.App/Paint/PhotoInputAlignmentPolicy.cs src/ClassroomToolkit.App/Paint/PhotoManipulationAdmissionPolicy.cs tests/ClassroomToolkit.Tests/PhotoTouchInteractionPolicyTests.cs tests/ClassroomToolkit.Tests/PhotoInputAlignmentPolicyTests.cs
git commit -m "refactor: separate touch ownership from manipulation routing"
```

## Task 2: Add Touch-Specific Release Tuning To The Shared Inertia Core

**Files:**
- Create: `src/ClassroomToolkit.App/Paint/PhotoPanReleaseTuning.cs`
- Create: `src/ClassroomToolkit.App/Paint/PhotoPanReleaseTuningPolicy.cs`
- Modify: `src/ClassroomToolkit.App/Paint/PhotoPanInertiaMotionPolicy.cs`
- Create: `tests/ClassroomToolkit.Tests/PhotoPanReleaseTuningPolicyTests.cs`
- Modify: `tests/ClassroomToolkit.Tests/PhotoPanInertiaMotionPolicyTests.cs`

- [ ] **Step 1: Write the failing tuning tests**

```csharp
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoPanReleaseTuningPolicyTests
{
    [Fact]
    public void ResolveTouch_ShouldLowerThreshold_AndIncreaseTravelComparedToMouse()
    {
        var baseTuning = PhotoPanInertiaTuning.Default;

        var mouse = PhotoPanReleaseTuningPolicy.Resolve(PhotoPanPointerKind.Mouse, baseTuning);
        var touch = PhotoPanReleaseTuningPolicy.Resolve(PhotoPanPointerKind.Touch, baseTuning);

        touch.MinReleaseSpeedDipPerMs.Should().BeLessThan(mouse.MinReleaseSpeedDipPerMs);
        touch.MaxReleaseSpeedDipPerMs.Should().BeGreaterThan(mouse.MaxReleaseSpeedDipPerMs);
        touch.DecelerationDipPerMs2.Should().BeLessThan(mouse.DecelerationDipPerMs2);
        touch.MaxDurationMs.Should().BeGreaterThan(mouse.MaxDurationMs);
        touch.MaxTranslationPerFrameDip.Should().BeGreaterThan(mouse.MaxTranslationPerFrameDip);
    }
}
```

```csharp
[Fact]
public void TryResolveReleaseVelocity_ShouldRespectTouchThreshold()
{
    var tuning = PhotoPanReleaseTuningPolicy.Resolve(
        PhotoPanPointerKind.Touch,
        PhotoPanInertiaTuning.Default);

    var resolved = PhotoPanInertiaMotionPolicy.TryResolveReleaseVelocity(
        new[]
        {
            new PhotoPanVelocitySample(new Point(0, 0), 1000),
            new PhotoPanVelocitySample(new Point(1.5, 0), 1030),
            new PhotoPanVelocitySample(new Point(4.5, 0), 1060)
        },
        releaseTimestampTicks: 1060,
        stopwatchFrequency: 1000,
        tuning,
        out var velocityDipPerMs);

    resolved.Should().BeTrue();
    velocityDipPerMs.X.Should().BeGreaterThan(0);
}
```

- [ ] **Step 2: Run the targeted tests to verify they fail**

Run:

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoPanReleaseTuningPolicyTests|FullyQualifiedName~PhotoPanInertiaMotionPolicyTests"
```

Expected:

- FAIL because `PhotoPanPointerKind`, `PhotoPanReleaseTuning`, and `PhotoPanReleaseTuningPolicy` do not exist
- FAIL because `PhotoPanInertiaMotionPolicy` has no overload that accepts release tuning

- [ ] **Step 3: Add pointer-kind-aware release tuning**

```csharp
namespace ClassroomToolkit.App.Paint;

internal enum PhotoPanPointerKind
{
    Mouse,
    Stylus,
    Touch
}

internal readonly record struct PhotoPanReleaseTuning(
    double DecelerationDipPerMs2,
    double StopSpeedDipPerMs,
    double MinReleaseSpeedDipPerMs,
    double MaxReleaseSpeedDipPerMs,
    double MaxDurationMs,
    double MaxTranslationPerFrameDip);
```

```csharp
using System;

namespace ClassroomToolkit.App.Paint;

internal static class PhotoPanReleaseTuningPolicy
{
    internal static PhotoPanReleaseTuning Resolve(
        PhotoPanPointerKind pointerKind,
        PhotoPanInertiaTuning tuning)
    {
        return pointerKind switch
        {
            PhotoPanPointerKind.Touch => new PhotoPanReleaseTuning(
                DecelerationDipPerMs2: tuning.MouseDecelerationDipPerMs2 * 0.74,
                StopSpeedDipPerMs: tuning.MouseStopSpeedDipPerMs,
                MinReleaseSpeedDipPerMs: Math.Max(0.03, tuning.MouseMinReleaseSpeedDipPerMs * 0.58),
                MaxReleaseSpeedDipPerMs: Math.Min(6.2, tuning.MouseMaxReleaseSpeedDipPerMs * 1.22),
                MaxDurationMs: Math.Max(1350.0, tuning.MouseMaxDurationMs * 1.15),
                MaxTranslationPerFrameDip: Math.Max(210.0, tuning.MouseMaxTranslationPerFrameDip * 1.2)),
            _ => new PhotoPanReleaseTuning(
                DecelerationDipPerMs2: tuning.MouseDecelerationDipPerMs2,
                StopSpeedDipPerMs: tuning.MouseStopSpeedDipPerMs,
                MinReleaseSpeedDipPerMs: tuning.MouseMinReleaseSpeedDipPerMs,
                MaxReleaseSpeedDipPerMs: tuning.MouseMaxReleaseSpeedDipPerMs,
                MaxDurationMs: tuning.MouseMaxDurationMs,
                MaxTranslationPerFrameDip: tuning.MouseMaxTranslationPerFrameDip)
        };
    }
}
```

```csharp
internal static class PhotoPanInertiaMotionPolicy
{
    internal static bool TryResolveReleaseVelocity(
        IReadOnlyList<PhotoPanVelocitySample> samples,
        long releaseTimestampTicks,
        long stopwatchFrequency,
        PhotoPanReleaseTuning tuning,
        out Vector velocityDipPerMs)
    {
        velocityDipPerMs = default;
        if (samples == null
            || samples.Count < 2
            || releaseTimestampTicks <= 0
            || stopwatchFrequency <= 0)
        {
            return false;
        }

        var lastSample = samples[^1];
        var sampleAgeMs = (releaseTimestampTicks - lastSample.TimestampTicks) * 1000.0 / stopwatchFrequency;
        if (sampleAgeMs > PhotoPanInertiaDefaults.MouseMaxVelocitySampleAgeMs)
        {
            return false;
        }

        var velocityWindowTicks = (long)Math.Ceiling(
            PhotoPanInertiaDefaults.MouseVelocitySampleWindowMs * stopwatchFrequency / 1000.0);
        var minAllowedTimestampTicks = Math.Max(0, lastSample.TimestampTicks - velocityWindowTicks);

        var weightedVelocity = new Vector();
        var totalWeight = 0.0;
        for (var i = 1; i < samples.Count; i++)
        {
            var previous = samples[i - 1];
            var current = samples[i];
            if (current.TimestampTicks <= previous.TimestampTicks || current.TimestampTicks < minAllowedTimestampTicks)
            {
                continue;
            }

            var elapsedMs = (current.TimestampTicks - previous.TimestampTicks) * 1000.0 / stopwatchFrequency;
            var effectiveElapsedMs = Math.Max(elapsedMs, PhotoPanInertiaDefaults.MouseMinVelocitySampleIntervalMs);
            var delta = current.Position - previous.Position;
            if (delta.Length < PhotoPanInertiaDefaults.MouseMinVelocitySampleDistanceDip)
            {
                continue;
            }

            var ageMs = (lastSample.TimestampTicks - current.TimestampTicks) * 1000.0 / stopwatchFrequency;
            var clampedAgeMs = Math.Clamp(ageMs, 0, PhotoPanInertiaDefaults.MouseVelocitySampleWindowMs);
            var recencyFactor = 1.0 + (
                (PhotoPanInertiaDefaults.MouseVelocitySampleWindowMs - clampedAgeMs)
                / PhotoPanInertiaDefaults.MouseVelocitySampleWindowMs
                * PhotoPanInertiaDefaults.MouseVelocityRecentWeightGain);

            weightedVelocity += new Vector(delta.X / effectiveElapsedMs, delta.Y / effectiveElapsedMs) * recencyFactor;
            totalWeight += recencyFactor;
        }

        if (totalWeight <= 0)
        {
            return false;
        }

        var rawVelocity = weightedVelocity / totalWeight;
        var speed = rawVelocity.Length;
        if (speed < tuning.MinReleaseSpeedDipPerMs)
        {
            return false;
        }

        if (speed > tuning.MaxReleaseSpeedDipPerMs)
        {
            rawVelocity *= tuning.MaxReleaseSpeedDipPerMs / speed;
        }

        velocityDipPerMs = rawVelocity;
        return true;
    }

    internal static Vector ResolveTranslation(
        Vector velocityDipPerMs,
        double elapsedMs,
        PhotoPanReleaseTuning tuning)
    {
        if (elapsedMs <= 0 || velocityDipPerMs.LengthSquared <= 0)
        {
            return default;
        }

        var translation = new Vector(
            velocityDipPerMs.X * elapsedMs,
            velocityDipPerMs.Y * elapsedMs);
        var distance = translation.Length;
        if (distance <= 0)
        {
            return default;
        }

        if (distance > tuning.MaxTranslationPerFrameDip)
        {
            translation *= tuning.MaxTranslationPerFrameDip / distance;
        }

        return translation;
    }

    internal static bool ShouldStopByDuration(double durationMs, PhotoPanReleaseTuning tuning)
    {
        return durationMs >= tuning.MaxDurationMs;
    }

    internal static Vector ResolveVelocityAfterDeceleration(
        Vector velocityDipPerMs,
        double elapsedMs,
        PhotoPanReleaseTuning tuning)
    {
        if (elapsedMs <= 0 || velocityDipPerMs.LengthSquared <= 0)
        {
            return velocityDipPerMs;
        }

        var speed = velocityDipPerMs.Length;
        var nextSpeed = speed - (tuning.DecelerationDipPerMs2 * elapsedMs);
        if (nextSpeed <= tuning.StopSpeedDipPerMs)
        {
            return default;
        }
        return velocityDipPerMs * (nextSpeed / speed);
    }
}
```

- [ ] **Step 4: Run the targeted tests to verify they pass**

Run:

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoPanReleaseTuningPolicyTests|FullyQualifiedName~PhotoPanInertiaMotionPolicyTests"
```

Expected:

- PASS for the new release tuning tests
- PASS for the updated inertia motion tests

- [ ] **Step 5: Commit**

```powershell
git add src/ClassroomToolkit.App/Paint/PhotoPanReleaseTuning.cs src/ClassroomToolkit.App/Paint/PhotoPanReleaseTuningPolicy.cs src/ClassroomToolkit.App/Paint/PhotoPanInertiaMotionPolicy.cs tests/ClassroomToolkit.Tests/PhotoPanReleaseTuningPolicyTests.cs tests/ClassroomToolkit.Tests/PhotoPanInertiaMotionPolicyTests.cs
git commit -m "feat: add touch-specific pan release tuning"
```

## Task 3: Add Single-Touch Input Plumbing And Promoted-Touch Stylus Filtering

**Files:**
- Create: `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Touch.cs`
- Modify: `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.State.cs`
- Modify: `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs`
- Modify: `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Lifecycle.cs`
- Modify: `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Stylus.cs`
- Create: `tests/ClassroomToolkit.Tests/PhotoTouchInputContractTests.cs`

- [ ] **Step 1: Write the failing input-plumbing contract test**

```csharp
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoTouchInputContractTests
{
    [Fact]
    public void PhotoTouchInput_ShouldRegisterTouchHandlers_AndIgnorePromotedStylusTouch()
    {
        var source = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow*.cs");

        source.Should().Contain("OverlayRoot.TouchDown += OnTouchDown;");
        source.Should().Contain("OverlayRoot.TouchMove += OnTouchMove;");
        source.Should().Contain("OverlayRoot.TouchUp += OnTouchUp;");
        source.Should().Contain("OverlayRoot.LostTouchCapture += OnOverlayLostTouchCapture;");
        source.Should().Contain("PhotoTouchInteractionPolicy.ShouldIgnorePromotedTouchStylus");
        source.Should().Contain("BeginPhotoPan(");
        source.Should().Contain("PhotoPanPointerKind.Touch");
    }
}
```

- [ ] **Step 2: Run the targeted test to verify it fails**

Run:

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoTouchInputContractTests"
```

Expected:

- FAIL because touch handlers and promoted-touch stylus filtering are not present yet

- [ ] **Step 3: Add touch runtime state and event hooks**

```csharp
private readonly HashSet<int> _photoActiveTouchIds = new();
private int? _photoTouchPanDeviceId;
private PhotoPanPointerKind _photoPanActivePointerKind = PhotoPanPointerKind.Mouse;
```

```csharp
OverlayRoot.TouchDown += OnTouchDown;
OverlayRoot.TouchMove += OnTouchMove;
OverlayRoot.TouchUp += OnTouchUp;
OverlayRoot.LostTouchCapture += OnOverlayLostTouchCapture;
```

```csharp
OverlayRoot.TouchDown -= OnTouchDown;
OverlayRoot.TouchMove -= OnTouchMove;
OverlayRoot.TouchUp -= OnTouchUp;
OverlayRoot.LostTouchCapture -= OnOverlayLostTouchCapture;
```

```csharp
private void OnStylusDown(object sender, StylusDownEventArgs e)
{
    if (PhotoTouchInteractionPolicy.ShouldIgnorePromotedTouchStylus(e.StylusDevice.TabletDevice.Type))
    {
        return;
    }
}
```

```csharp
using System.Windows.Input;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void OnTouchDown(object sender, TouchEventArgs e)
    {
        _photoActiveTouchIds.Add(e.TouchDevice.Id);
        StopPhotoPanInertia(flushTransformSave: false, resetInkPanCompensation: false);

        if (!PhotoTouchInteractionPolicy.ShouldUseSingleTouchPan(
                _photoModeActive,
                IsBoardActive(),
                _mode,
                IsInkOperationActive(),
                _photoActiveTouchIds.Count))
        {
            return;
        }

        _photoTouchPanDeviceId = e.TouchDevice.Id;
        OverlayRoot.CaptureTouch(e.TouchDevice);
        BeginPhotoPan(
            e.GetTouchPoint(OverlayRoot).Position,
            PhotoPanPointerKind.Touch,
            captureStylus: false);
        MarkPhotoGestureInput();
        e.Handled = true;
    }

    private void OnTouchMove(object sender, TouchEventArgs e)
    {
        if (_photoTouchPanDeviceId != e.TouchDevice.Id || _photoActiveTouchIds.Count != 1)
        {
            return;
        }

        UpdatePhotoPan(e.GetTouchPoint(OverlayRoot).Position);
        MarkPhotoGestureInput();
        e.Handled = true;
    }

    private void OnTouchUp(object sender, TouchEventArgs e)
    {
        _photoActiveTouchIds.Remove(e.TouchDevice.Id);
        if (_photoTouchPanDeviceId != e.TouchDevice.Id)
        {
            return;
        }

        UpdatePhotoPanVelocitySamples(e.GetTouchPoint(OverlayRoot).Position);
        OverlayRoot.ReleaseTouchCapture(e.TouchDevice);
        EndPhotoPan();
        _photoTouchPanDeviceId = null;
        e.Handled = true;
    }

    private void OnOverlayLostTouchCapture(object sender, TouchEventArgs e)
    {
        _photoActiveTouchIds.Remove(e.TouchDevice.Id);
        if (_photoTouchPanDeviceId == e.TouchDevice.Id)
        {
            _photoTouchPanDeviceId = null;
            EndPhotoPan(allowInertia: false);
        }
    }
}
```

- [ ] **Step 4: Run the targeted contract test to verify it passes**

Run:

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoTouchInputContractTests"
```

Expected:

- PASS because touch hooks, touch state, and promoted-touch stylus suppression are all visible in source

- [ ] **Step 5: Commit**

```powershell
git add src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Touch.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.State.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Lifecycle.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Stylus.cs tests/ClassroomToolkit.Tests/PhotoTouchInputContractTests.cs
git commit -m "feat: add touch pan input plumbing"
```

## Task 4: Move Single-Touch Flick Onto The Shared Inertia Core

**Files:**
- Modify: `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Manipulation.cs`
- Modify: `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.PanInertia.cs`
- Modify: `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Telemetry.cs`
- Modify: `tests/ClassroomToolkit.Tests/PhotoManipulationInertiaPolicyTests.cs`
- Modify: `tests/ClassroomToolkit.Tests/PhotoPanInertiaRenderingContractTests.cs`
- Modify: `tests/ClassroomToolkit.Tests/PhotoPanInertiaMotionPolicyTests.cs`

- [ ] **Step 1: Write the failing shared-inertia contract and motion tests**

```csharp
[Fact]
public void PhotoPanInertia_ShouldResolveReleaseTuningFromActivePointerKind()
{
    var source = ContractSourceAggregateLoader.LoadByPattern(
        "src",
        "ClassroomToolkit.App",
        "Paint",
        "PaintOverlayWindow.Photo.Transform*.cs");

    source.Should().Contain("PhotoPanReleaseTuningPolicy.Resolve(_photoPanActivePointerKind, _photoPanInertiaTuning)");
}
```

```csharp
[Fact]
public void ResolveTranslation_ShouldHonorTouchReleaseTuningFrameClamp()
{
    var tuning = PhotoPanReleaseTuningPolicy.Resolve(
        PhotoPanPointerKind.Touch,
        PhotoPanInertiaTuning.Default);

    var translation = PhotoPanInertiaMotionPolicy.ResolveTranslation(
        new Vector(6.0, 0),
        elapsedMs: 80,
        tuning);

    translation.Length.Should().BeLessThanOrEqualTo(tuning.MaxTranslationPerFrameDip);
}
```

- [ ] **Step 2: Run the targeted tests to verify they fail**

Run:

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoManipulationInertiaPolicyTests|FullyQualifiedName~PhotoPanInertiaRenderingContractTests|FullyQualifiedName~PhotoPanInertiaMotionPolicyTests"
```

Expected:

- FAIL because the pan inertia startup path still assumes mouse/stylus tuning semantics
- FAIL because manipulation still owns single-touch movement

- [ ] **Step 3: Bind active pointer kind and move touch flick startup to the custom inertia pipeline**

```csharp
private bool TryBeginPhotoPan(MouseButtonEventArgs e)
{
    var shouldPanPhoto = StylusCursorPolicy.ShouldPanPhoto(
        _photoModeActive,
        IsBoardActive(),
        _mode,
        IsInkOperationActive());
    if (!PhotoPanBeginGuardPolicy.ShouldBegin(shouldPanPhoto, _photoPanning))
    {
        return false;
    }

    BeginPhotoPan(
        e.GetPosition(OverlayRoot),
        PhotoPanPointerKind.Mouse,
        captureStylus: false);
    e.Handled = true;
    return true;
}
```

```csharp
private void BeginPhotoPan(
    WpfPoint position,
    PhotoPanPointerKind pointerKind,
    bool captureStylus)
{
    StopPhotoPanInertia(flushTransformSave: false, resetInkPanCompensation: false);
    _photoPanActivePointerKind = pointerKind;
    _photoPanning = true;
    _photoPanHadEffectiveMovement = false;
    _photoPanStart = position;
    _photoPanOriginX = _photoTranslate.X;
    _photoPanOriginY = _photoTranslate.Y;
    MarkPhotoInteractionForRenderQuality();
    ResetPhotoPanVelocitySamples(position);
    SyncPhotoInteractiveRefreshAnchor();
}
```

```csharp
private bool TryStartPhotoPanInertiaFromRelease()
{
    var nowTicks = Stopwatch.GetTimestamp();
    var releaseTuning = PhotoPanReleaseTuningPolicy.Resolve(
        _photoPanActivePointerKind,
        _photoPanInertiaTuning);
    if (!PhotoPanInertiaMotionPolicy.TryResolveReleaseVelocity(
            _photoPanVelocitySamples,
            nowTicks,
            Stopwatch.Frequency,
            releaseTuning,
            out var velocityDipPerMs))
    {
        return false;
    }

    _photoPanInertiaVelocityDipPerMs = velocityDipPerMs;
    var nowUtc = GetCurrentUtcTimestamp();
    _photoPanInertiaLastTickUtc = nowUtc;
    _photoPanInertiaStartUtc = nowUtc;
    _photoPanInertiaLastRenderingTime = TimeSpan.MinValue;
    if (!_photoPanInertiaRenderingAttached)
    {
        CompositionTarget.Rendering += OnPhotoPanInertiaRendering;
        _photoPanInertiaRenderingAttached = true;
    }
    return true;
}
```

```csharp
private void OnManipulationStarting(object? sender, ManipulationStartingEventArgs e)
{
    if (!PhotoTouchInteractionPolicy.ShouldUseManipulationZoom(_photoActiveTouchIds.Count))
    {
        e.Handled = _photoModeActive;
        return;
    }

    StopPhotoPanInertia(flushTransformSave: false, resetInkPanCompensation: false);
    _photoManipulating = true;
    e.ManipulationContainer = OverlayRoot;
    e.Mode = ManipulationModes.Scale | ManipulationModes.Translate;
}
```

```csharp
private void OnManipulationInertiaStarting(object? sender, ManipulationInertiaStartingEventArgs e)
{
    if (!PhotoTouchInteractionPolicy.ShouldUseManipulationZoom(_photoActiveTouchIds.Count))
    {
        e.Handled = _photoModeActive;
        return;
    }

    _photoManipulating = true;
    e.TranslationBehavior.DesiredDeceleration = PhotoManipulationInertiaPolicy.ResolveTranslationDeceleration(
        CaptureInputInteractionState().CrossPageDisplayActive,
        _photoPanInertiaTuning);
    e.Handled = true;
}
```

- [ ] **Step 4: Add immediate stop and multi-touch handoff behavior**

```csharp
private void OnTouchDown(object sender, TouchEventArgs e)
{
    _photoActiveTouchIds.Add(e.TouchDevice.Id);
    StopPhotoPanInertia(flushTransformSave: false, resetInkPanCompensation: false);

    if (_photoTouchPanDeviceId.HasValue && _photoActiveTouchIds.Count > 1)
    {
        EndPhotoPan(allowInertia: false);
        _photoTouchPanDeviceId = null;
        MarkPhotoGestureInput();
        return;
    }

    if (!PhotoTouchInteractionPolicy.ShouldUseSingleTouchPan(
            _photoModeActive,
            IsBoardActive(),
            _mode,
            IsInkOperationActive(),
            _photoActiveTouchIds.Count))
    {
        return;
    }

    _photoTouchPanDeviceId = e.TouchDevice.Id;
    OverlayRoot.CaptureTouch(e.TouchDevice);
    BeginPhotoPan(
        e.GetTouchPoint(OverlayRoot).Position,
        PhotoPanPointerKind.Touch,
        captureStylus: false);
    LogPhotoInputTelemetry("touch-pan-start", $"touchId={e.TouchDevice.Id}");
    e.Handled = true;
}
```

```csharp
private void OnPhotoPanInertiaRendering(object? sender, EventArgs e)
{
    if (!_photoModeActive || _photoPanning || _photoPanInertiaLastTickUtc == PhotoInputConflictDefaults.UnsetTimestampUtc)
    {
        StopPhotoPanInertia(flushTransformSave: true, resetInkPanCompensation: true);
        return;
    }

    var nowUtc = GetCurrentUtcTimestamp();
    var durationMs = (nowUtc - _photoPanInertiaStartUtc).TotalMilliseconds;
    var releaseTuning = PhotoPanReleaseTuningPolicy.Resolve(
        _photoPanActivePointerKind,
        _photoPanInertiaTuning);

    if (PhotoPanInertiaMotionPolicy.ShouldStopByDuration(durationMs, releaseTuning))
    {
        StopPhotoPanInertia(flushTransformSave: true, resetInkPanCompensation: true);
        return;
    }

    var elapsedMs = (nowUtc - _photoPanInertiaLastTickUtc).TotalMilliseconds;
    if (e is RenderingEventArgs renderingArgs
        && _photoPanInertiaLastRenderingTime != TimeSpan.MinValue
        && renderingArgs.RenderingTime > _photoPanInertiaLastRenderingTime)
    {
        elapsedMs = (renderingArgs.RenderingTime - _photoPanInertiaLastRenderingTime).TotalMilliseconds;
    }
    _photoPanInertiaLastRenderingTime = e is RenderingEventArgs currentArgs
        ? currentArgs.RenderingTime
        : _photoPanInertiaLastRenderingTime;
    elapsedMs = PhotoPanInertiaMotionPolicy.ResolveFrameElapsedMilliseconds(elapsedMs);
    _photoPanInertiaLastTickUtc = nowUtc;

    var translation = PhotoPanInertiaMotionPolicy.ResolveTranslation(
        _photoPanInertiaVelocityDipPerMs,
        elapsedMs,
        releaseTuning);
    if (translation.LengthSquared <= 0)
    {
        StopPhotoPanInertia(flushTransformSave: true, resetInkPanCompensation: true);
        return;
    }

    _photoTranslate.X += translation.X;
    _photoTranslate.Y += translation.Y;
    ApplyPhotoPanBounds(allowResistance: false);

    _photoPanInertiaVelocityDipPerMs = PhotoPanInertiaMotionPolicy.ResolveVelocityAfterDeceleration(
        _photoPanInertiaVelocityDipPerMs,
        elapsedMs,
        releaseTuning);
}
```

- [ ] **Step 5: Run the targeted tests to verify they pass**

Run:

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoManipulationInertiaPolicyTests|FullyQualifiedName~PhotoPanInertiaRenderingContractTests|FullyQualifiedName~PhotoPanInertiaMotionPolicyTests|FullyQualifiedName~PhotoTouchInputContractTests"
```

Expected:

- PASS for pointer-kind-aware inertia startup
- PASS for manipulation remaining the multi-touch zoom owner
- PASS for the rendering-loop inertia contract

- [ ] **Step 6: Commit**

```powershell
git add src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Manipulation.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.PanInertia.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Telemetry.cs tests/ClassroomToolkit.Tests/PhotoManipulationInertiaPolicyTests.cs tests/ClassroomToolkit.Tests/PhotoPanInertiaRenderingContractTests.cs tests/ClassroomToolkit.Tests/PhotoPanInertiaMotionPolicyTests.cs
git commit -m "feat: unify touch flick with shared inertia core"
```

## Task 5: Run Full Gates, Record Evidence, And Freeze The Change

**Files:**
- Create: `docs/change-evidence/20260419-photo-touch-flick-inertia.md`

- [ ] **Step 1: Write the evidence file before the final gate run**

```markdown
# 变更证据：PDF/图片触屏单指甩动惯性优化（2026-04-19）

- 规则 ID：R1/R2/R6/R8
- 风险等级：中
- 当前落点：`PaintOverlayWindow` 触屏单指甩动与跨页惯性
- 目标归宿：单指触屏走共享惯性内核；双指缩放保持 manipulation；新触点立即刹停

## Commands / Evidence

- `codex --version`
- `codex --help`
- `codex status`
- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`

## Hotspot Review

- `PaintOverlayWindow.Input.Touch.cs`
- `PaintOverlayWindow.Input.Manipulation.cs`
- `PaintOverlayWindow.Photo.Transform.PanInertia.cs`
- `PhotoPanInertiaMotionPolicy.cs`

## Rollback

- `git restore --source=HEAD~1 -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Touch.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Manipulation.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.PanInertia.cs src/ClassroomToolkit.App/Paint/PhotoPanInertiaMotionPolicy.cs`
```

- [ ] **Step 2: Run the required repository gates in the correct order**

Run:

```powershell
dotnet build ClassroomToolkit.sln -c Debug
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
```

Expected:

- build: `0 Warning(s), 0 Error(s)`
- full tests: PASS
- contract/invariant subset: PASS

- [ ] **Step 3: Perform hotspot review and complete the evidence file**

Write these exact conclusions into `docs/change-evidence/20260419-photo-touch-flick-inertia.md` after the gate output:

```markdown
- 复核点 1：单指触屏和双指缩放是否抢占同一手势。
  - 结论：单指由 `TouchDown/Move/Up` 拥有；`Manipulation` 仅在 `activeTouchCount >= 2` 时接管。
- 复核点 2：新触点是否能立即终止惯性。
  - 结论：`OnTouchDown()` 首先调用 `StopPhotoPanInertia(...)`，且多指接管前会 `EndPhotoPan(allowInertia: false)`。
- 复核点 3：跨页模式是否沿用共享惯性而不引入缩放/刷新回归。
  - 结论：触屏甩动仍复用原有 `CompositionTarget.Rendering` 平移循环、邻页 transform 同步和延后补刷路径。
- 复核点 4：鼠标/触控笔兼容是否被保留。
  - 结论：鼠标/触控笔仍走自定义惯性核心；仅新增 promoted-touch stylus 过滤，避免手指双重输入。
```

- [ ] **Step 4: Commit the evidence**

```powershell
git add docs/change-evidence/20260419-photo-touch-flick-inertia.md
git commit -m "docs: record photo touch flick inertia evidence"
```

- [ ] **Step 5: Report the exact verification summary**

Use this close-out structure in the implementation session:

```markdown
Implemented touch-first photo/PDF flick inertia with single-touch ownership, shared pan inertia, and immediate stop on new touch-down.

Verification:
- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
```
