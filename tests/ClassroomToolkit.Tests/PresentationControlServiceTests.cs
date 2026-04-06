using ClassroomToolkit.Interop.Presentation;
using ClassroomToolkit.Services.Presentation;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PresentationControlServiceTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenPlannerIsNull()
    {
        Action act = () => new PresentationControlService(
            null!,
            new PresentationCommandMapper(),
            new RecordingInputSender(),
            new Win32PresentationResolver(),
            new MockValidator());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenMapperIsNull()
    {
        Action act = () => new PresentationControlService(
            new PresentationControlPlanner(new PresentationClassifier()),
            null!,
            new RecordingInputSender(),
            new Win32PresentationResolver(),
            new MockValidator());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenInputSenderIsNull()
    {
        Action act = () => new PresentationControlService(
            new PresentationControlPlanner(new PresentationClassifier()),
            new PresentationCommandMapper(),
            null!,
            new Win32PresentationResolver(),
            new MockValidator());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenResolverIsNull()
    {
        Action act = () => new PresentationControlService(
            new PresentationControlPlanner(new PresentationClassifier()),
            new PresentationCommandMapper(),
            new RecordingInputSender(),
            null!,
            new MockValidator());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenValidatorIsNull()
    {
        Action act = () => new PresentationControlService(
            new PresentationControlPlanner(new PresentationClassifier()),
            new PresentationCommandMapper(),
            new RecordingInputSender(),
            new Win32PresentationResolver(),
            null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TrySendForeground_ShouldThrow_WhenOptionsIsNull()
    {
        var service = new PresentationControlService(
            new PresentationControlPlanner(new PresentationClassifier()),
            new PresentationCommandMapper(),
            new RecordingInputSender(),
            new Win32PresentationResolver(),
            new MockValidator());

        Action act = () => service.TrySendForeground(PresentationCommand.Next, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TrySendToTarget_ShouldThrow_WhenOptionsIsNull()
    {
        var service = new PresentationControlService(
            new PresentationControlPlanner(new PresentationClassifier()),
            new PresentationCommandMapper(),
            new RecordingInputSender(),
            new Win32PresentationResolver(),
            new MockValidator());
        var target = new PresentationTarget(new IntPtr(1234), new PresentationWindowInfo(1, "wpspresentation.exe", new[] { "wpsshowframe" }));

        Action act = () => service.TrySendToTarget(target, PresentationCommand.Next, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TrySendToTarget_ShouldReturnFalse_WhenValidatorThrowsRecoverableException()
    {
        var service = new PresentationControlService(
            new PresentationControlPlanner(new PresentationClassifier()),
            new PresentationCommandMapper(),
            new RecordingInputSender(),
            new Win32PresentationResolver(),
            new ThrowingValidator(new InvalidOperationException("validator-boom")));
        var target = new PresentationTarget(
            new IntPtr(1234),
            new PresentationWindowInfo(1, "wpspresentation.exe", new[] { "wpsshowframe" }));
        var options = new PresentationControlOptions
        {
            Strategy = InputStrategy.Auto,
            AllowWps = true
        };

        var result = service.TrySendToTarget(target, PresentationCommand.Next, options);

        result.Should().BeFalse();
    }

    [Fact]
    public void TrySendToTarget_ShouldRethrowFatalException_WhenValidatorThrowsFatalException()
    {
        var service = new PresentationControlService(
            new PresentationControlPlanner(new PresentationClassifier()),
            new PresentationCommandMapper(),
            new RecordingInputSender(),
            new Win32PresentationResolver(),
            new ThrowingValidator(new BadImageFormatException("fatal-validator")));
        var target = new PresentationTarget(
            new IntPtr(1234),
            new PresentationWindowInfo(1, "wpspresentation.exe", new[] { "wpsshowframe" }));
        var options = new PresentationControlOptions
        {
            Strategy = InputStrategy.Auto,
            AllowWps = true
        };

        var act = () => service.TrySendToTarget(target, PresentationCommand.Next, options);

        act.Should().Throw<BadImageFormatException>();
    }

    [Fact]
    public void TrySendForeground_ShouldReturnFalse_WhenResolverThrowsRecoverableException()
    {
        var service = new PresentationControlService(
            new PresentationControlPlanner(new PresentationClassifier()),
            new PresentationCommandMapper(),
            new RecordingInputSender(),
            new ThrowingResolver(new InvalidOperationException("resolver-boom")),
            new MockValidator());
        var options = new PresentationControlOptions
        {
            Strategy = InputStrategy.Auto,
            AllowWps = true,
            AllowOffice = true
        };

        var result = service.TrySendForeground(PresentationCommand.Next, options);

        result.Should().BeFalse();
    }

    [Fact]
    public void TrySendForeground_ShouldRethrowFatalException_WhenResolverThrowsFatalException()
    {
        var service = new PresentationControlService(
            new PresentationControlPlanner(new PresentationClassifier()),
            new PresentationCommandMapper(),
            new RecordingInputSender(),
            new ThrowingResolver(new BadImageFormatException("resolver-fatal")),
            new MockValidator());
        var options = new PresentationControlOptions
        {
            Strategy = InputStrategy.Auto,
            AllowWps = true,
            AllowOffice = true
        };

        var act = () => service.TrySendForeground(PresentationCommand.Next, options);

        act.Should().Throw<BadImageFormatException>();
    }

    [Fact]
    public void WpsWheelForwardDisabled_WhenDowngradedToMessage_ShouldSendKeyDownOnly()
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
        sender.KeyCalls.Should().Be(1);
        sender.LastKeyDownOnly.Should().BeTrue();
        sender.LastKey.Should().Be(VirtualKey.PageDown);
        sender.LastKeyStrategy.Should().Be(InputStrategy.Message);
        sender.WheelCalls.Should().Be(0);
    }

    [Fact]
    public void WpsWheelForwardDisabled_WhenRawAvailable_ShouldStillSendKeyDownOnly()
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
        var info = new PresentationWindowInfo(1, "wpspresentation.exe", new[] { "wpsshowframe" });
        var target = new PresentationTarget(new IntPtr(1234), info);
        var options = new PresentationControlOptions
        {
            Strategy = InputStrategy.Raw,
            WheelAsKey = false,
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

    [Fact]
    public void WpsDebounceMs_Zero_ShouldAllowImmediateRepeatedNavigation()
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
            AllowWps = true,
            WpsDebounceMs = 0
        };

        var first = service.TrySendToTarget(target, PresentationCommand.Next, options);
        var second = service.TrySendToTarget(target, PresentationCommand.Next, options);

        first.Should().BeTrue();
        second.Should().BeTrue();
        sender.KeyCalls.Should().Be(2);
    }

    [Fact]
    public void WpsRawFallback_WhenLockDisabled_ShouldRetryRawOnNextCommand()
    {
        var planner = new PresentationControlPlanner(new PresentationClassifier());
        var mapper = new PresentationCommandMapper();
        var sender = new StrategyAwareInputSender();
        var resolver = new Win32PresentationResolver();
        var validator = new MockValidator();
        var service = new PresentationControlService(
            planner,
            mapper,
            sender,
            resolver,
            validator,
            new StubForegroundController(initialForeground: true, ensureResult: true));
        var info = new PresentationWindowInfo(1, "wpspresentation.exe", new[] { "wpsshowframe" });
        var target = new PresentationTarget(new IntPtr(1234), info);
        var options = new PresentationControlOptions
        {
            Strategy = InputStrategy.Raw,
            WheelAsKey = true,
            AllowWps = true,
            LockStrategyWhenDegraded = false,
            WpsDebounceMs = 0
        };

        var first = service.TrySendToTarget(target, PresentationCommand.Next, options);
        var second = service.TrySendToTarget(target, PresentationCommand.Next, options);

        first.Should().BeTrue();
        second.Should().BeTrue();
        sender.RawKeyAttempts.Should().Be(2);
        sender.MessageKeyAttempts.Should().Be(2);
    }

    [Fact]
    public void WpsRawFallback_WithLockEnabled_ShouldLockAfterTwoConsecutiveFailures()
    {
        var planner = new PresentationControlPlanner(new PresentationClassifier());
        var mapper = new PresentationCommandMapper();
        var sender = new StrategyAwareInputSender();
        var resolver = new Win32PresentationResolver();
        var validator = new MockValidator();
        var service = new PresentationControlService(
            planner,
            mapper,
            sender,
            resolver,
            validator,
            new StubForegroundController(initialForeground: true, ensureResult: true));
        var target = new PresentationTarget(
            new IntPtr(1234),
            new PresentationWindowInfo(1, "wpspresentation.exe", new[] { "wpsshowframe" }));
        var options = new PresentationControlOptions
        {
            Strategy = InputStrategy.Raw,
            WheelAsKey = true,
            AllowWps = true,
            LockStrategyWhenDegraded = true,
            WpsDebounceMs = 0
        };

        var first = service.TrySendToTarget(target, PresentationCommand.Next, options);
        var second = service.TrySendToTarget(target, PresentationCommand.Next, options);
        var third = service.TrySendToTarget(target, PresentationCommand.Next, options);

        first.Should().BeTrue();
        second.Should().BeTrue();
        third.Should().BeTrue();
        sender.RawKeyAttempts.Should().Be(2);
        sender.MessageKeyAttempts.Should().Be(3);
        service.IsWpsAutoForcedMessageForTarget(target.Handle).Should().BeTrue();
    }

    [Fact]
    public void WpsRawFallback_WithLockEnabled_ShouldNotAffectDifferentTarget()
    {
        var planner = new PresentationControlPlanner(new PresentationClassifier());
        var mapper = new PresentationCommandMapper();
        var sender = new HandleAwareStrategyInputSender();
        sender.SetRawResult(new IntPtr(1111), succeed: false);
        sender.SetRawResult(new IntPtr(2222), succeed: true);
        var resolver = new Win32PresentationResolver();
        var validator = new MockValidator();
        var service = new PresentationControlService(
            planner,
            mapper,
            sender,
            resolver,
            validator,
            new StubForegroundController(initialForeground: true, ensureResult: true));
        var targetA = new PresentationTarget(
            new IntPtr(1111),
            new PresentationWindowInfo(1, "wpspresentation.exe", new[] { "wpsshowframe" }));
        var targetB = new PresentationTarget(
            new IntPtr(2222),
            new PresentationWindowInfo(1, "wpspresentation.exe", new[] { "wpsshowframe" }));
        var options = new PresentationControlOptions
        {
            Strategy = InputStrategy.Raw,
            WheelAsKey = true,
            AllowWps = true,
            LockStrategyWhenDegraded = true,
            WpsDebounceMs = 0
        };

        service.TrySendToTarget(targetA, PresentationCommand.Next, options).Should().BeTrue();
        service.TrySendToTarget(targetA, PresentationCommand.Next, options).Should().BeTrue();
        service.IsWpsAutoForcedMessageForTarget(targetA.Handle).Should().BeTrue();

        service.TrySendToTarget(targetB, PresentationCommand.Next, options).Should().BeTrue();
        sender.RawAttemptsByHandle[targetB.Handle].Should().Be(1);
        service.IsWpsAutoForcedMessageForTarget(targetB.Handle).Should().BeFalse();
    }

    [Fact]
    public void WpsRawFallback_WithLockEnabled_ShouldAutoProbeAndRecoverAfterMessageWindow()
    {
        var planner = new PresentationControlPlanner(new PresentationClassifier());
        var mapper = new PresentationCommandMapper();
        var sender = new HandleAwareStrategyInputSender();
        var targetHandle = new IntPtr(3333);
        sender.SetRawResult(targetHandle, succeed: false);
        var resolver = new Win32PresentationResolver();
        var validator = new MockValidator();
        var service = new PresentationControlService(
            planner,
            mapper,
            sender,
            resolver,
            validator,
            new StubForegroundController(initialForeground: true, ensureResult: true));
        var target = new PresentationTarget(
            targetHandle,
            new PresentationWindowInfo(1, "wpspresentation.exe", new[] { "wpsshowframe" }));
        var options = new PresentationControlOptions
        {
            Strategy = InputStrategy.Raw,
            WheelAsKey = true,
            AllowWps = true,
            LockStrategyWhenDegraded = true,
            WpsDebounceMs = 0
        };

        // 先触发锁定（2次 raw 失败）。
        service.TrySendToTarget(target, PresentationCommand.Next, options).Should().BeTrue();
        service.TrySendToTarget(target, PresentationCommand.Next, options).Should().BeTrue();
        service.IsWpsAutoForcedMessageForTarget(targetHandle).Should().BeTrue();
        sender.RawAttemptsByHandle[targetHandle].Should().Be(2);

        // 锁定期间累计 message 成功，尚未到探活窗口前不应再尝试 raw。
        sender.SetRawResult(targetHandle, succeed: true);
        for (var i = 0; i < 8; i++)
        {
            service.TrySendToTarget(target, PresentationCommand.Next, options).Should().BeTrue();
        }
        sender.RawAttemptsByHandle[targetHandle].Should().Be(2);

        // 到达探活窗口后，应自动尝试 raw 并恢复。
        service.TrySendToTarget(target, PresentationCommand.Next, options).Should().BeTrue();
        sender.RawAttemptsByHandle[targetHandle].Should().Be(3);
        service.IsWpsAutoForcedMessageForTarget(targetHandle).Should().BeFalse();

        // 恢复后下一次继续 raw 主路径。
        service.TrySendToTarget(target, PresentationCommand.Next, options).Should().BeTrue();
        sender.RawAttemptsByHandle[targetHandle].Should().Be(4);
    }

    [Fact]
    public void WpsRawFallback_WithCustomThreshold_ShouldDelayLockUntilConfiguredFailures()
    {
        var planner = new PresentationControlPlanner(new PresentationClassifier());
        var mapper = new PresentationCommandMapper();
        var sender = new StrategyAwareInputSender();
        var resolver = new Win32PresentationResolver();
        var validator = new MockValidator();
        var service = new PresentationControlService(
            planner,
            mapper,
            sender,
            resolver,
            validator,
            new StubForegroundController(initialForeground: true, ensureResult: true));
        var target = new PresentationTarget(
            new IntPtr(7777),
            new PresentationWindowInfo(1, "wpspresentation.exe", new[] { "wpsshowframe" }));
        var options = new PresentationControlOptions
        {
            Strategy = InputStrategy.Raw,
            WheelAsKey = true,
            AllowWps = true,
            LockStrategyWhenDegraded = true,
            AutoFallbackFailureThreshold = 3,
            WpsDebounceMs = 0
        };

        service.TrySendToTarget(target, PresentationCommand.Next, options).Should().BeTrue();
        service.TrySendToTarget(target, PresentationCommand.Next, options).Should().BeTrue();
        service.IsWpsAutoForcedMessageForTarget(target.Handle).Should().BeFalse();
        service.TrySendToTarget(target, PresentationCommand.Next, options).Should().BeTrue();
        service.IsWpsAutoForcedMessageForTarget(target.Handle).Should().BeTrue();

        sender.RawKeyAttempts.Should().Be(3);
        sender.MessageKeyAttempts.Should().Be(3);
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

    private sealed class ThrowingValidator : IPresentationWindowValidator
    {
        private readonly Exception _exception;

        public ThrowingValidator(Exception exception)
        {
            _exception = exception;
        }

        public bool IsWindowValid(IntPtr hwnd)
        {
            throw _exception;
        }
    }

    private sealed class ThrowingResolver : IPresentationTargetResolver
    {
        private readonly Exception _exception;

        public ThrowingResolver(Exception exception)
        {
            _exception = exception;
        }

        public PresentationTarget ResolveForeground()
        {
            throw _exception;
        }

        public PresentationTarget ResolvePresentationTarget(
            PresentationClassifier classifier,
            bool allowWps,
            bool allowOffice,
            uint? excludeProcessId = null)
        {
            throw _exception;
        }
    }

    private sealed class StrategyAwareInputSender : IInputSender
    {
        public int RawKeyAttempts { get; private set; }
        public int MessageKeyAttempts { get; private set; }

        public bool SendKey(IntPtr hwnd, VirtualKey key, KeyModifiers modifiers, InputStrategy strategy, bool keyDownOnly)
        {
            if (strategy == InputStrategy.Raw)
            {
                RawKeyAttempts++;
                return false;
            }

            MessageKeyAttempts++;
            return true;
        }

        public bool SendWheel(IntPtr hwnd, int delta, InputStrategy strategy)
        {
            return false;
        }
    }

    private sealed class HandleAwareStrategyInputSender : IInputSender
    {
        private readonly Dictionary<IntPtr, bool> _rawResultByHandle = new();
        public Dictionary<IntPtr, int> RawAttemptsByHandle { get; } = new();
        public Dictionary<IntPtr, int> MessageAttemptsByHandle { get; } = new();

        public void SetRawResult(IntPtr handle, bool succeed)
        {
            _rawResultByHandle[handle] = succeed;
        }

        public bool SendKey(IntPtr hwnd, VirtualKey key, KeyModifiers modifiers, InputStrategy strategy, bool keyDownOnly)
        {
            if (strategy == InputStrategy.Raw)
            {
                RawAttemptsByHandle[hwnd] = RawAttemptsByHandle.TryGetValue(hwnd, out var count)
                    ? count + 1
                    : 1;
                return _rawResultByHandle.TryGetValue(hwnd, out var succeed) && succeed;
            }

            MessageAttemptsByHandle[hwnd] = MessageAttemptsByHandle.TryGetValue(hwnd, out var messageCount)
                ? messageCount + 1
                : 1;
            return true;
        }

        public bool SendWheel(IntPtr hwnd, int delta, InputStrategy strategy)
        {
            return false;
        }
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
