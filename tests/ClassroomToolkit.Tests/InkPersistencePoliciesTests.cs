using AwesomeAssertions;
using ClassroomToolkit.App.Paint;
using System;
using System.Collections.Generic;
using System.Windows;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class InkAutoSaveSnapshotAdmissionPolicyTests
{
    [Fact]
    public void ShouldPersistSnapshot_ShouldReturnTrue_WhenRuntimeStateIsUnknown()
    {
        var shouldPersist = InkPersistencePolicies.ShouldPersistSnapshot(
            runtimeStateKnown: false,
            runtimeHash: string.Empty,
            snapshotHash: "strokes-v1");

        shouldPersist.Should().BeTrue();
    }

    [Fact]
    public void ShouldPersistSnapshot_ShouldReturnTrue_WhenRuntimeHashMatchesSnapshot()
    {
        var shouldPersist = InkPersistencePolicies.ShouldPersistSnapshot(
            runtimeStateKnown: true,
            runtimeHash: "empty",
            snapshotHash: "empty");

        shouldPersist.Should().BeTrue();
    }

    [Fact]
    public void ShouldPersistSnapshot_ShouldReturnFalse_WhenRuntimeHashAdvanced()
    {
        var shouldPersist = InkPersistencePolicies.ShouldPersistSnapshot(
            runtimeStateKnown: true,
            runtimeHash: "empty",
            snapshotHash: "strokes-v1");

        shouldPersist.Should().BeFalse();
    }
}

public sealed class InkCacheUpdateTransitionPolicyTests
{
    [Fact]
    public void Resolve_ShouldStartMonitor_WhenMonitorDisabled()
    {
        var plan = InkPersistencePolicies.ResolveInkCacheUpdateTransition(
            enabled: true,
            monitorEnabled: false);

        plan.ShouldStartMonitor.Should().BeTrue();
        plan.ShouldClearCache.Should().BeFalse();
        plan.ShouldRequestRefresh.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldClearCache_WhenDisabled()
    {
        var plan = InkPersistencePolicies.ResolveInkCacheUpdateTransition(
            enabled: false,
            monitorEnabled: true);

        plan.ShouldStartMonitor.Should().BeFalse();
        plan.ShouldClearCache.Should().BeTrue();
        plan.ShouldRequestRefresh.Should().BeTrue();
    }
}

public sealed class InkRedrawClipPolicyTests
{
    [Fact]
    public void ShouldUsePartialClear_ShouldReturnTrue_WhenClipMatchesLastFrame()
    {
        var clip = new Int32Rect(10, 20, 300, 200);

        var result = InkRedrawPolicies.ShouldUsePartialClear(
            clipAvailable: true,
            clipPixelRect: clip,
            lastClipPixelRect: clip);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldUsePartialClear_ShouldReturnFalse_WhenClipChanged()
    {
        var result = InkRedrawPolicies.ShouldUsePartialClear(
            clipAvailable: true,
            clipPixelRect: new Int32Rect(10, 20, 300, 200),
            lastClipPixelRect: new Int32Rect(10, 20, 301, 200));

        result.Should().BeFalse();
    }

    [Fact]
    public void TryResolvePixelClip_ShouldClampToSurfaceBounds()
    {
        var ok = InkRedrawPolicies.TryResolvePixelClip(
            clipBoundsDip: new Rect(-10, -20, 250, 180),
            surfacePixelWidth: 200,
            surfacePixelHeight: 120,
            surfaceDpiX: 96,
            surfaceDpiY: 96,
            out var rect);

        ok.Should().BeTrue();
        rect.Should().Be(new Int32Rect(0, 0, 200, 120));
    }
}

public sealed class InkRedrawTelemetryPolicyTests
{
    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("on")]
    [InlineData("yes")]
    [InlineData("enabled")]
    [InlineData(" enabled ")]
    public void IsEnabledValue_ShouldReturnTrue_ForTruthyValues(string raw)
    {
        InkRedrawTelemetryPolicies.IsEnabledValue(raw).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("false")]
    [InlineData("off")]
    [InlineData("no")]
    [InlineData("disabled")]
    public void IsEnabledValue_ShouldReturnFalse_ForFalsyValues(string? raw)
    {
        InkRedrawTelemetryPolicies.IsEnabledValue(raw).Should().BeFalse();
    }

    [Fact]
    public void AppendSample_ShouldTrimWindowToConfiguredSize()
    {
        var samples = new Queue<double>();
        InkRedrawTelemetryPolicies.AppendSample(samples, 1, windowSize: 3);
        InkRedrawTelemetryPolicies.AppendSample(samples, 2, windowSize: 3);
        InkRedrawTelemetryPolicies.AppendSample(samples, 3, windowSize: 3);
        InkRedrawTelemetryPolicies.AppendSample(samples, 4, windowSize: 3);

        samples.Should().Equal(new[] { 2.0, 3.0, 4.0 });
    }

    [Fact]
    public void Percentile_ShouldReturnP50AndP95FromSortedSamples()
    {
        var samples = new[] { 5.0, 1.0, 3.0, 2.0, 4.0 };

        var p50 = InkRedrawTelemetryPolicies.Percentile(samples, 0.5);
        var p95 = InkRedrawTelemetryPolicies.Percentile(samples, 0.95);

        p50.Should().Be(3.0);
        p95.Should().Be(4.0);
    }

    [Fact]
    public void ShouldEmitLog_ShouldRespectStrideAndInterval()
    {
        var now = DateTime.UtcNow;
        var recent = now.AddSeconds(-5);

        var blocked = InkRedrawTelemetryPolicies.ShouldEmitLog(
            sampleCount: 5,
            nowUtc: now,
            lastLogUtc: recent,
            minSampleStride: 40,
            minIntervalSeconds: 30);
        blocked.Should().BeFalse();

        var strideHit = InkRedrawTelemetryPolicies.ShouldEmitLog(
            sampleCount: 40,
            nowUtc: now,
            lastLogUtc: recent,
            minSampleStride: 40,
            minIntervalSeconds: 30);
        strideHit.Should().BeTrue();

        var intervalHit = InkRedrawTelemetryPolicies.ShouldEmitLog(
            sampleCount: 5,
            nowUtc: now,
            lastLogUtc: now.AddSeconds(-31),
            minSampleStride: 40,
            minIntervalSeconds: 30);
        intervalHit.Should().BeTrue();
    }
}

public sealed class InkSaveUpdateTransitionPolicyTests
{
    [Fact]
    public void Resolve_ShouldStopTimer_WhenDisabled()
    {
        var plan = InkPersistencePolicies.ResolveInkSaveUpdateTransition(enabled: false);

        plan.ShouldStopAutoSaveTimer.Should().BeTrue();
        plan.ShouldCancelPendingAutoSave.Should().BeTrue();
        plan.ShouldScheduleAutoSave.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldScheduleAutoSave_WhenEnabled()
    {
        var plan = InkPersistencePolicies.ResolveInkSaveUpdateTransition(enabled: true);

        plan.ShouldStopAutoSaveTimer.Should().BeFalse();
        plan.ShouldCancelPendingAutoSave.Should().BeFalse();
        plan.ShouldScheduleAutoSave.Should().BeTrue();
    }
}

public sealed class InkShowUpdateTransitionPolicyTests
{
    [Fact]
    public void Resolve_ShouldSkip_WhenStateUnchanged()
    {
        var plan = InkPersistencePolicies.ResolveInkShowUpdateTransition(
            currentInkShowEnabled: true,
            nextInkShowEnabled: true,
            photoModeActive: true);

        plan.ShouldApplySetting.Should().BeFalse();
        plan.ShouldReturnAfterSetting.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldApplyAndReturn_WhenPhotoModeInactive()
    {
        var plan = InkPersistencePolicies.ResolveInkShowUpdateTransition(
            currentInkShowEnabled: false,
            nextInkShowEnabled: true,
            photoModeActive: false);

        plan.ShouldApplySetting.Should().BeTrue();
        plan.ShouldReturnAfterSetting.Should().BeTrue();
        plan.ShouldLoadCurrentPage.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldClearState_WhenDisablingInPhotoMode()
    {
        var plan = InkPersistencePolicies.ResolveInkShowUpdateTransition(
            currentInkShowEnabled: true,
            nextInkShowEnabled: false,
            photoModeActive: true);

        plan.ShouldClearInkState.Should().BeTrue();
        plan.RequestCrossPageUpdateForDisabled.Should().BeTrue();
        plan.ShouldLoadCurrentPage.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldLoadState_WhenEnablingInPhotoMode()
    {
        var plan = InkPersistencePolicies.ResolveInkShowUpdateTransition(
            currentInkShowEnabled: false,
            nextInkShowEnabled: true,
            photoModeActive: true);

        plan.ShouldLoadCurrentPage.Should().BeTrue();
        plan.RequestCrossPageUpdateForEnabled.Should().BeTrue();
        plan.ShouldClearInkState.Should().BeFalse();
    }
}

public sealed class InkSidecarLoadAdmissionPolicyTests
{
    [Fact]
    public void ShouldApplyLoadedSnapshot_ShouldReturnTrue_WhenRuntimeStateUnknown()
    {
        var shouldApply = InkPersistencePolicies.ShouldApplyLoadedSnapshot(
            runtimeStateKnown: false,
            runtimeHash: string.Empty,
            runtimeDirty: false,
            loadedHash: "A");

        shouldApply.Should().BeTrue();
    }

    [Fact]
    public void ShouldApplyLoadedSnapshot_ShouldReturnTrue_WhenHashMatches()
    {
        var shouldApply = InkPersistencePolicies.ShouldApplyLoadedSnapshot(
            runtimeStateKnown: true,
            runtimeHash: "A",
            runtimeDirty: true,
            loadedHash: "A");

        shouldApply.Should().BeTrue();
    }

    [Fact]
    public void ShouldApplyLoadedSnapshot_ShouldReturnFalse_WhenRuntimeIsDirtyAndHashMismatches()
    {
        var shouldApply = InkPersistencePolicies.ShouldApplyLoadedSnapshot(
            runtimeStateKnown: true,
            runtimeHash: "A",
            runtimeDirty: true,
            loadedHash: "B");

        shouldApply.Should().BeFalse();
    }

    [Fact]
    public void ShouldApplyLoadedSnapshot_ShouldReturnFalse_WhenRuntimeIsClearedButLoadedIsNotEmpty()
    {
        var shouldApply = InkPersistencePolicies.ShouldApplyLoadedSnapshot(
            runtimeStateKnown: true,
            runtimeHash: "empty",
            runtimeDirty: false,
            loadedHash: "B");

        shouldApply.Should().BeFalse();
    }

    [Fact]
    public void ShouldApplyLoadedSnapshot_ShouldReturnTrue_WhenRuntimeIsCleanAndNonEmptyDespiteMismatch()
    {
        var shouldApply = InkPersistencePolicies.ShouldApplyLoadedSnapshot(
            runtimeStateKnown: true,
            runtimeHash: "A",
            runtimeDirty: false,
            loadedHash: "B");

        shouldApply.Should().BeTrue();
    }
}
