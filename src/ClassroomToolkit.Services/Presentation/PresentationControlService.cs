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

    public void ResetWpsAutoFallback()
    {
        _wpsAutoForceMessage = false;
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
        var strategy = options.Strategy;
        if (strategy == InputStrategy.Auto && _wpsAutoForceMessage)
        {
            strategy = InputStrategy.Message;
        }
        var sent = TrySendWithStrategy(target, command, options, strategy, out var targetType);
        if (!sent && options.Strategy == InputStrategy.Auto && targetType == PresentationType.Wps && !_wpsAutoForceMessage)
        {
            _wpsAutoForceMessage = true;
            sent = TrySendWithStrategy(target, command, options, InputStrategy.Message, out _);
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
        if (plan.TargetType == PresentationType.Wps && plan.Strategy == InputStrategy.Raw)
        {
            var isForeground = PresentationWindowFocus.IsForeground(target.Handle);
            if (!isForeground)
            {
                PresentationWindowFocus.EnsureForeground(target.Handle);
                isForeground = PresentationWindowFocus.IsForeground(target.Handle);
            }
            if (!isForeground)
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
}
