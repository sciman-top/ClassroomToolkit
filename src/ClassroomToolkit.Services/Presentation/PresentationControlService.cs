using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.Services.Presentation;

public sealed class PresentationControlService
{
    private readonly PresentationControlPlanner _planner;
    private readonly PresentationCommandMapper _mapper;
    private readonly IInputSender _inputSender;
    private readonly Win32PresentationResolver _resolver;
    private DateTime _lastWpsCommandAt = DateTime.MinValue;
    private PresentationCommand? _lastWpsCommandType;
    private IntPtr _lastWpsTarget = IntPtr.Zero;
    private PresentationTarget _lastTarget = PresentationTarget.Empty;
    private readonly uint _currentProcessId;
    private bool _wpsAutoForceMessage;
    private bool _officeAutoForceMessage;

    public PresentationControlService(
        PresentationControlPlanner planner,
        PresentationCommandMapper mapper,
        IInputSender inputSender,
        Win32PresentationResolver resolver)
    {
        _planner = planner;
        _mapper = mapper;
        _inputSender = inputSender;
        _resolver = resolver;
        _currentProcessId = (uint)Environment.ProcessId;
    }

    public bool IsWpsAutoForcedMessage => _wpsAutoForceMessage;
    public bool IsOfficeAutoForcedMessage => _officeAutoForceMessage;

    public void ResetWpsAutoFallback()
    {
        _wpsAutoForceMessage = false;
    }

    public void ResetOfficeAutoFallback()
    {
        _officeAutoForceMessage = false;
    }

    public bool TrySendForeground(PresentationCommand command, PresentationControlOptions options)
    {
        var target = _resolver.ResolveForeground();
        if (!target.IsValid || target.Info.ProcessId == _currentProcessId)
        {
            target = _lastTarget;
            if (!target.IsValid)
            {
                target = _resolver.ResolvePresentationTarget(
                    _planner.Classifier,
                    options.AllowWps,
                    options.AllowOffice,
                    _currentProcessId);
            }
        }
        if (!target.IsValid)
        {
            return false;
        }
        if (TrySendToTarget(target, command, options))
        {
            return true;
        }
        var fallback = _resolver.ResolvePresentationTarget(
            _planner.Classifier,
            options.AllowWps,
            options.AllowOffice,
            _currentProcessId);
        if (!fallback.IsValid || fallback.Equals(target))
        {
            return false;
        }
        return TrySendToTarget(fallback, command, options);
    }

    public bool TrySendToTarget(PresentationTarget target, PresentationCommand command, PresentationControlOptions options)
    {
        var targetType = _planner.Classifier.Classify(target.Info);
        if (targetType == PresentationType.Wps && !options.AllowWps)
        {
            return false;
        }
        if (targetType == PresentationType.Office && !options.AllowOffice)
        {
            return false;
        }
        if (targetType == PresentationType.None)
        {
            return false;
        }

        var strategy = options.Strategy;
        if (targetType == PresentationType.Wps && _wpsAutoForceMessage && strategy != InputStrategy.Message)
        {
            strategy = InputStrategy.Message;
        }
        if (targetType == PresentationType.Office && _officeAutoForceMessage && strategy != InputStrategy.Message)
        {
            strategy = InputStrategy.Message;
        }

        var sent = TrySendWithStrategy(target, command, options, strategy, out var effectiveType);
        if (!sent && strategy != InputStrategy.Message)
        {
            if (effectiveType == PresentationType.Wps)
            {
                _wpsAutoForceMessage = true;
                sent = TrySendWithStrategy(target, command, options, InputStrategy.Message, out _);
            }
            else if (effectiveType == PresentationType.Office)
            {
                _officeAutoForceMessage = true;
                sent = TrySendWithStrategy(target, command, options, InputStrategy.Message, out _);
            }
        }
        return sent;
    }

    private bool TrySendWithStrategy(
        PresentationTarget target,
        PresentationCommand command,
        PresentationControlOptions options,
        InputStrategy strategy,
        out PresentationType targetType)
    {
        var effective = new PresentationControlOptions
        {
            Strategy = strategy,
            WheelAsKey = options.WheelAsKey,
            AllowWps = options.AllowWps,
            AllowOffice = options.AllowOffice
        };
        var plan = _planner.Plan(target.Info, effective, command);
        if (plan == null)
        {
            targetType = PresentationType.None;
            return false;
        }
        targetType = plan.TargetType;
        if (plan.TargetType == PresentationType.Wps && IsWpsDebounced(command, target.Handle))
        {
            return false;
        }
        var keyDownOnly = plan.TargetType == PresentationType.Wps;
        if (plan.Strategy == InputStrategy.Raw && RequiresForeground(plan.TargetType))
        {
            if (!TryEnsureForeground(target.Handle))
            {
                plan = new PresentationControlPlan(plan.TargetType, InputStrategy.Message, plan.UseWheelAsKey);
            }
        }
        if (plan.TargetType == PresentationType.Wps
            && !plan.UseWheelAsKey
            && command is PresentationCommand.Next or PresentationCommand.Previous)
        {
            var delta = command == PresentationCommand.Next ? -120 : 120;
            var wheelStrategy = plan.Strategy == InputStrategy.Raw ? InputStrategy.Message : plan.Strategy;
            var wheelSent = _inputSender.SendWheel(target.Handle, delta, wheelStrategy);
            if (wheelSent)
            {
                _lastTarget = target;
                RememberWpsCommand(command, target.Handle);
            }
            return wheelSent;
        }
        var binding = _mapper.Map(plan.TargetType, command);
        var sent = _inputSender.SendKey(target.Handle, binding.Key, binding.Modifiers, plan.Strategy, keyDownOnly);
        if (sent)
        {
            _lastTarget = target;
            if (plan.TargetType == PresentationType.Wps)
            {
                RememberWpsCommand(command, target.Handle);
            }
        }
        return sent;
    }

    private bool IsWpsDebounced(PresentationCommand command, IntPtr target)
    {
        if (target == IntPtr.Zero || _lastWpsTarget != target)
        {
            return false;
        }
        if (_lastWpsCommandType != command)
        {
            return false;
        }
        var elapsed = DateTime.UtcNow - _lastWpsCommandAt;
        return elapsed.TotalMilliseconds < 200;
    }

    private void RememberWpsCommand(PresentationCommand command, IntPtr target)
    {
        _lastWpsCommandAt = DateTime.UtcNow;
        _lastWpsCommandType = command;
        _lastWpsTarget = target;
    }

    private static bool RequiresForeground(PresentationType targetType)
    {
        return targetType == PresentationType.Wps || targetType == PresentationType.Office;
    }

    private static bool TryEnsureForeground(IntPtr target)
    {
        var isForeground = PresentationWindowFocus.IsForeground(target);
        if (isForeground)
        {
            return true;
        }
        PresentationWindowFocus.EnsureForeground(target);
        return PresentationWindowFocus.IsForeground(target);
    }
}
