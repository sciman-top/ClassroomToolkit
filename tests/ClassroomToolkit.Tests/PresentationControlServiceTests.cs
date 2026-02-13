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
        var validator = new MockValidator();
        var service = new PresentationControlService(planner, mapper, sender, resolver, validator);
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
    public void WpsWheelForwardEnabled_ShouldSendKeyDownOnlyUsingMessageFallback()
    {
        var planner = new PresentationControlPlanner(new PresentationClassifier());
        var mapper = new PresentationCommandMapper();
        var sender = new RecordingInputSender();
        var resolver = new Win32PresentationResolver();
        var validator = new MockValidator();
        var service = new PresentationControlService(planner, mapper, sender, resolver, validator);
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
    public void Office_ShouldFallbackToMessageWhenNotForeground()
    {
        var planner = new PresentationControlPlanner(new PresentationClassifier());
        var mapper = new PresentationCommandMapper();
        var sender = new RecordingInputSender();
        var resolver = new Win32PresentationResolver();
        var validator = new MockValidator();
        var service = new PresentationControlService(
            planner,
            mapper,
            sender,
            resolver,
            validator,
            new StubForegroundController(initialForeground: false, ensureResult: false));
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
        sender.LastKeyStrategy.Should().Be(InputStrategy.Message);
        sender.WheelCalls.Should().Be(0);
    }

    [Fact]
    public void Office_RawStrategy_ShouldFallbackToMessage_WhenForegroundCannotBeEnsured()
    {
        var planner = new PresentationControlPlanner(new PresentationClassifier());
        var mapper = new PresentationCommandMapper();
        var sender = new RecordingInputSender();
        var resolver = new Win32PresentationResolver();
        var validator = new MockValidator();
        var service = new PresentationControlService(
            planner,
            mapper,
            sender,
            resolver,
            validator,
            new StubForegroundController(initialForeground: false, ensureResult: false));
        var info = new PresentationWindowInfo(2, "powerpnt.exe", new[] { "screenclass" });
        var target = new PresentationTarget(new IntPtr(5678), info);
        var options = new PresentationControlOptions
        {
            Strategy = InputStrategy.Raw,
            WheelAsKey = true,
            AllowOffice = true
        };

        var result = service.TrySendToTarget(target, PresentationCommand.Next, options);

        result.Should().BeTrue();
        sender.KeyCalls.Should().Be(1);
        sender.LastKeyStrategy.Should().Be(InputStrategy.Message);
    }

    [Fact]
    public void Office_RawStrategy_ShouldKeepRaw_WhenAlreadyForeground()
    {
        var planner = new PresentationControlPlanner(new PresentationClassifier());
        var mapper = new PresentationCommandMapper();
        var sender = new RecordingInputSender();
        var resolver = new Win32PresentationResolver();
        var validator = new MockValidator();
        var service = new PresentationControlService(
            planner,
            mapper,
            sender,
            resolver,
            validator,
            new StubForegroundController(initialForeground: true, ensureResult: true));
        var info = new PresentationWindowInfo(2, "powerpnt.exe", new[] { "screenclass" });
        var target = new PresentationTarget(new IntPtr(5678), info);
        var options = new PresentationControlOptions
        {
            Strategy = InputStrategy.Raw,
            WheelAsKey = true,
            AllowOffice = true
        };

        var result = service.TrySendToTarget(target, PresentationCommand.Next, options);

        result.Should().BeTrue();
        sender.KeyCalls.Should().Be(1);
        sender.LastKeyStrategy.Should().Be(InputStrategy.Raw);
    }

    [Fact]
    public void OtherWindowType_ShouldNotSendAnyInput()
    {
        var planner = new PresentationControlPlanner(new PresentationClassifier());
        var mapper = new PresentationCommandMapper();
        var sender = new RecordingInputSender();
        var resolver = new Win32PresentationResolver();
        var validator = new MockValidator();
        var service = new PresentationControlService(planner, mapper, sender, resolver, validator);
        var info = new PresentationWindowInfo(3, "notepad.exe", new[] { "Notepad" });
        var target = new PresentationTarget(new IntPtr(9012), info);
        var options = new PresentationControlOptions
        {
            Strategy = InputStrategy.Auto,
            WheelAsKey = true,
            AllowWps = true,
            AllowOffice = true
        };

        var result = service.TrySendToTarget(target, PresentationCommand.Next, options);

        result.Should().BeFalse();
        sender.KeyCalls.Should().Be(0);
        sender.WheelCalls.Should().Be(0);
    }

    [Fact]
    public void Wps_FirstCommand_ShouldSendHomeKeyDownOnly()
    {
        var planner = new PresentationControlPlanner(new PresentationClassifier());
        var mapper = new PresentationCommandMapper();
        var sender = new RecordingInputSender();
        var resolver = new Win32PresentationResolver();
        var validator = new MockValidator();
        var service = new PresentationControlService(planner, mapper, sender, resolver, validator);
        var info = new PresentationWindowInfo(1, "wpspresentation.exe", new[] { "wpsshowframe" });
        var target = new PresentationTarget(new IntPtr(1234), info);
        var options = new PresentationControlOptions
        {
            Strategy = InputStrategy.Auto,
            WheelAsKey = false,
            AllowWps = true
        };

        var result = service.TrySendToTarget(target, PresentationCommand.First, options);

        result.Should().BeTrue();
        sender.KeyCalls.Should().Be(1);
        sender.LastKey.Should().Be(VirtualKey.Home);
        sender.LastKeyDownOnly.Should().BeTrue();
        sender.WheelCalls.Should().Be(0);
    }

    [Fact]
    public void Office_LastCommand_ShouldSendEndKey()
    {
        var planner = new PresentationControlPlanner(new PresentationClassifier());
        var mapper = new PresentationCommandMapper();
        var sender = new RecordingInputSender();
        var resolver = new Win32PresentationResolver();
        var validator = new MockValidator();
        var service = new PresentationControlService(
            planner,
            mapper,
            sender,
            resolver,
            validator,
            new StubForegroundController(initialForeground: true, ensureResult: true));
        var info = new PresentationWindowInfo(2, "powerpnt.exe", new[] { "screenclass" });
        var target = new PresentationTarget(new IntPtr(5678), info);
        var options = new PresentationControlOptions
        {
            Strategy = InputStrategy.Raw,
            WheelAsKey = true,
            AllowOffice = true
        };

        var result = service.TrySendToTarget(target, PresentationCommand.Last, options);

        result.Should().BeTrue();
        sender.KeyCalls.Should().Be(1);
        sender.LastKey.Should().Be(VirtualKey.End);
        sender.LastKeyDownOnly.Should().BeFalse();
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

    private sealed class MockValidator : IPresentationWindowValidator
    {
        public bool IsWindowValid(IntPtr hwnd) => true;
    }

    private sealed class StubForegroundController : IForegroundWindowController
    {
        private bool _isForeground;
        private readonly bool _ensureResult;

        public StubForegroundController(bool initialForeground, bool ensureResult)
        {
            _isForeground = initialForeground;
            _ensureResult = ensureResult;
        }

        public bool IsForeground(IntPtr hwnd) => _isForeground;

        public bool EnsureForeground(IntPtr hwnd)
        {
            if (_ensureResult)
            {
                _isForeground = true;
                return true;
            }
            return false;
        }
    }
}
