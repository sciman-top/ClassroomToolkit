using ClassroomToolkit.Interop.Presentation;
using ClassroomToolkit.Services.Presentation;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PresentationControlServiceTests
{
    [Fact]
    public void WpsWheelForwardDisabled_ShouldSendWheel()
    {
        var planner = new PresentationControlPlanner(new PresentationClassifier());
        var mapper = new PresentationCommandMapper();
        var sender = new RecordingInputSender();
        var resolver = new Win32PresentationResolver();
        var service = new PresentationControlService(planner, mapper, sender, resolver);
        var info = new PresentationWindowInfo(1, "wpspresentation.exe", new[] { "wpsshowframe" });
        var target = new PresentationTarget(new IntPtr(1234), info);
        var options = new PresentationControlOptions
        {
            Strategy = InputStrategy.Auto,
            WheelAsKey = false,
            AllowWps = true
        };

        var result = service.TrySendToTarget(target, PresentationCommand.Next, options);

        result.Should().BeTrue();
        sender.WheelCalls.Should().Be(1);
        sender.LastWheelDelta.Should().Be(-120);
        sender.LastWheelStrategy.Should().Be(InputStrategy.Message);
        sender.KeyCalls.Should().Be(0);
    }

    [Fact]
    public void WpsWheelForwardEnabled_ShouldSendKeyDownOnly()
    {
        var planner = new PresentationControlPlanner(new PresentationClassifier());
        var mapper = new PresentationCommandMapper();
        var sender = new RecordingInputSender();
        var resolver = new Win32PresentationResolver();
        var service = new PresentationControlService(planner, mapper, sender, resolver);
        var info = new PresentationWindowInfo(1, "wpspresentation.exe", new[] { "wpsshowframe" });
        var target = new PresentationTarget(new IntPtr(1234), info);
        var options = new PresentationControlOptions
        {
            Strategy = InputStrategy.Auto,
            WheelAsKey = true,
            AllowWps = true
        };

        var result = service.TrySendToTarget(target, PresentationCommand.Next, options);

        result.Should().BeTrue();
        sender.KeyCalls.Should().Be(1);
        sender.LastKeyDownOnly.Should().BeTrue();
        sender.LastKey.Should().Be(VirtualKey.PageDown);
        sender.LastKeyStrategy.Should().Be(InputStrategy.Message);
        sender.WheelCalls.Should().Be(0);
    }

    [Fact]
    public void Office_ShouldSendKeyWithKeyUp()
    {
        var planner = new PresentationControlPlanner(new PresentationClassifier());
        var mapper = new PresentationCommandMapper();
        var sender = new RecordingInputSender();
        var resolver = new Win32PresentationResolver();
        var service = new PresentationControlService(planner, mapper, sender, resolver);
        var info = new PresentationWindowInfo(2, "powerpnt.exe", new[] { "screenclass" });
        var target = new PresentationTarget(new IntPtr(5678), info);
        var options = new PresentationControlOptions
        {
            Strategy = InputStrategy.Auto,
            WheelAsKey = true,
            AllowOffice = true
        };

        var result = service.TrySendToTarget(target, PresentationCommand.Next, options);

        result.Should().BeTrue();
        sender.KeyCalls.Should().Be(1);
        sender.LastKeyDownOnly.Should().BeFalse();
        sender.LastKey.Should().Be(VirtualKey.PageDown);
        sender.LastKeyStrategy.Should().Be(InputStrategy.Raw);
        sender.WheelCalls.Should().Be(0);
    }

    private sealed class RecordingInputSender : IInputSender
    {
        public int KeyCalls { get; private set; }
        public int WheelCalls { get; private set; }
        public bool LastKeyDownOnly { get; private set; }
        public VirtualKey LastKey { get; private set; }
        public KeyModifiers LastModifiers { get; private set; }
        public InputStrategy LastKeyStrategy { get; private set; }
        public int LastWheelDelta { get; private set; }
        public InputStrategy LastWheelStrategy { get; private set; }

        public bool SendKey(IntPtr hwnd, VirtualKey key, KeyModifiers modifiers, InputStrategy strategy, bool keyDownOnly)
        {
            KeyCalls++;
            LastKey = key;
            LastModifiers = modifiers;
            LastKeyStrategy = strategy;
            LastKeyDownOnly = keyDownOnly;
            return true;
        }

        public bool SendWheel(IntPtr hwnd, int delta, InputStrategy strategy)
        {
            WheelCalls++;
            LastWheelDelta = delta;
            LastWheelStrategy = strategy;
            return true;
        }
    }
}
