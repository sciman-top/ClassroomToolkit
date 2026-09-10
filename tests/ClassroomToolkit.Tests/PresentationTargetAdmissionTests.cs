using AwesomeAssertions;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.Interop.Presentation;
using ClassroomToolkit.Services.Presentation;

namespace ClassroomToolkit.Tests;

public sealed class PresentationTargetAdmissionTests
{
    [Fact]
    public void IsFreshIdentityMatch_ShouldRejectRecycledHandleWithDifferentChannel()
    {
        var target = new PresentationTarget(
            new IntPtr(100),
            new PresentationWindowInfo(1, "wpspresentation.exe", ["wpsshowframe"]));
        var currentCheck = new PresentationWindowCheck(
            PresentationType.Office,
            ProcessId: 2,
            "powerpnt.exe",
            ["screenclass"],
            ClassMatch: true,
            ProcessMatch: true,
            HasCaption: false,
            IsFullscreen: true,
            Score: 100);

        PresentationTargetAdmissionPolicy.IsFreshIdentityMatch(
                target,
                currentCheck,
                new PresentationClassifier(),
                expectedType: null)
            .Should().BeFalse();
    }

    [Fact]
    public void IsFreshIdentityMatch_ShouldAcceptSameChannelWithCurrentEvidence()
    {
        var target = new PresentationTarget(
            new IntPtr(100),
            new PresentationWindowInfo(1, "wpspresentation.exe", ["wpsshowframe"]));
        var currentCheck = new PresentationWindowCheck(
            PresentationType.Wps,
            ProcessId: 1,
            "wpspresentation.exe",
            ["wpsshowframe"],
            ClassMatch: true,
            ProcessMatch: true,
            HasCaption: false,
            IsFullscreen: true,
            Score: 100);

        PresentationTargetAdmissionPolicy.IsFreshIdentityMatch(
                target,
                currentCheck,
                new PresentationClassifier(),
                expectedType: PresentationType.Wps)
            .Should().BeTrue();
    }

    [Fact]
    public void IsFreshIdentityMatch_ShouldRejectSameChannelFromDifferentProcess()
    {
        var target = CreateWpsTarget(processId: 1);
        var currentCheck = CreateWpsCheck(processId: 2);

        PresentationTargetAdmissionPolicy.IsFreshIdentityMatch(
                target,
                currentCheck,
                new PresentationClassifier(),
                expectedType: PresentationType.Wps)
            .Should().BeFalse();
    }

    [Fact]
    public void IsFreshIdentityMatch_ShouldRejectSameProcessWithDifferentClassIdentity()
    {
        var target = CreateWpsTarget(processId: 1);
        var currentCheck = new PresentationWindowCheck(
            PresentationType.Wps,
            ProcessId: 1,
            "wpspresentation.exe",
            ["different-show-frame"],
            ClassMatch: false,
            ProcessMatch: true,
            HasCaption: false,
            IsFullscreen: true,
            Score: 100);

        PresentationTargetAdmissionPolicy.IsFreshIdentityMatch(
                target,
                currentCheck,
                new PresentationClassifier(),
                expectedType: PresentationType.Wps)
            .Should().BeFalse();
    }

    [Fact]
    public void IsFreshIdentityMatch_ShouldRejectSamePidWithDifferentProcessIdentity()
    {
        var target = CreateWpsTarget(processId: 1);
        var currentCheck = new PresentationWindowCheck(
            PresentationType.Wps,
            ProcessId: 1,
            "different-presentation.exe",
            ["wpsshowframe"],
            ClassMatch: true,
            ProcessMatch: true,
            HasCaption: false,
            IsFullscreen: true,
            Score: 100);

        PresentationTargetAdmissionPolicy.IsFreshIdentityMatch(
                target,
                currentCheck,
                new PresentationClassifier(),
                expectedType: PresentationType.Wps)
            .Should().BeFalse();
    }

    private static PresentationTarget CreateWpsTarget(uint processId)
    {
        return new PresentationTarget(
            new IntPtr(100),
            new PresentationWindowInfo(processId, "wpspresentation.exe", ["wpsshowframe"]));
    }

    private static PresentationWindowCheck CreateWpsCheck(uint processId)
    {
        return new PresentationWindowCheck(
            PresentationType.Wps,
            ProcessId: processId,
            "wpspresentation.exe",
            ["wpsshowframe"],
            ClassMatch: true,
            ProcessMatch: true,
            HasCaption: false,
            IsFullscreen: true,
            Score: 100);
    }

    [Fact]
    public void TrySendToTarget_ShouldFailClosedBeforeInputInjection_WhenAdmissionRejectsTarget()
    {
        var sender = new RecordingInputSender();
        var service = new PresentationControlService(
            new PresentationControlPlanner(new PresentationClassifier()),
            new PresentationCommandMapper(),
            sender,
            new EmptyResolver(),
            new AlwaysValidWindowValidator(),
            targetAdmission: _ => false);
        var target = new PresentationTarget(
            new IntPtr(100),
            new PresentationWindowInfo(1, "wpspresentation.exe", ["wpsshowframe"]));

        var sent = service.TrySendToTarget(
            target,
            PresentationCommand.Next,
            new PresentationControlOptions { AllowWps = true });

        sent.Should().BeFalse();
        sender.SendKeyCalls.Should().Be(0);
    }

    private sealed class RecordingInputSender : IInputSender
    {
        public int SendKeyCalls { get; private set; }

        public bool SendKey(
            IntPtr hwnd,
            VirtualKey key,
            KeyModifiers modifiers,
            InputStrategy strategy,
            bool keyDownOnly)
        {
            SendKeyCalls++;
            return true;
        }

        public bool SendWheel(IntPtr hwnd, int delta, InputStrategy strategy) => false;
    }

    private sealed class EmptyResolver : IPresentationTargetResolver
    {
        public PresentationTarget ResolveForeground() => PresentationTarget.Empty;

        public PresentationTarget ResolvePresentationTarget(
            PresentationClassifier classifier,
            bool allowWps,
            bool allowOffice,
            uint? excludeProcessId = null) => PresentationTarget.Empty;
    }

    private sealed class AlwaysValidWindowValidator : IPresentationWindowValidator
    {
        public bool IsWindowValid(IntPtr hwnd) => true;
    }
}
