using ClassroomToolkit.Interop.Presentation;
using System.Diagnostics;

namespace ClassroomToolkit.Services.Presentation;

public sealed class PresentationControlService
{
    private readonly PresentationControlPlanner _planner;
    private readonly PresentationCommandMapper _mapper;
    private readonly IInputSender _inputSender;
    private readonly IPresentationTargetResolver _resolver;
    private readonly IPresentationWindowValidator _validator;
    private long _lastWpsCommandTick;
    private PresentationCommand? _lastWpsCommandType;
    private IntPtr _lastWpsTarget = IntPtr.Zero;
    private readonly uint _currentProcessId;
    private bool _wpsAutoForceMessage;
    private bool _officeAutoForceMessage;
    private readonly IForegroundWindowController _foregroundController;

    public PresentationControlService(
        PresentationControlPlanner planner,
        PresentationCommandMapper mapper,
        IInputSender inputSender,
        IPresentationTargetResolver resolver,
        IPresentationWindowValidator validator,
        IForegroundWindowController? foregroundController = null)
    {
        ArgumentNullException.ThrowIfNull(planner);
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(inputSender);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(validator);

        _planner = planner;
        _mapper = mapper;
        _inputSender = inputSender;
        _resolver = resolver;
        _validator = validator;
        _foregroundController = foregroundController ?? new PresentationForegroundController();
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
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            return TrySendForegroundCore(command, options);
        }
        catch (Exception ex) when (PresentationExceptionFilterPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine(
                PresentationControlDiagnosticsPolicy.FormatSendFailureMessage(
                    operation: "foreground",
                    command,
                    target: IntPtr.Zero,
                    exceptionType: ex.GetType().Name,
                    message: ex.Message));
            return false;
        }
    }

    private bool TrySendForegroundCore(PresentationCommand command, PresentationControlOptions options)
    {
        var target = _resolver.ResolveForeground();
        if (!IsForegroundCandidate(target, options))
        {
            target = _resolver.ResolvePresentationTarget(
                _planner.Classifier,
                options.AllowWps,
                options.AllowOffice,
                _currentProcessId);
        }
        if (!IsTargetHandleValid(target))
        {
            target = _resolver.ResolvePresentationTarget(
                _planner.Classifier,
                options.AllowWps,
                options.AllowOffice,
                _currentProcessId);
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

    private bool IsForegroundCandidate(PresentationTarget target, PresentationControlOptions options)
    {
        if (!target.IsValid || target.Info.ProcessId == _currentProcessId)
        {
            return false;
        }
        var targetType = _planner.Classifier.Classify(target.Info);
        if (targetType is PresentationType.None or PresentationType.Other)
        {
            return false;
        }
        if (targetType == PresentationType.Wps && !options.AllowWps)
        {
            return false;
        }
        if (targetType == PresentationType.Office && !options.AllowOffice)
        {
            return false;
        }
        return true;
    }

    public bool TrySendToTarget(PresentationTarget target, PresentationCommand command, PresentationControlOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            return TrySendToTargetCore(target, command, options);
        }
        catch (Exception ex) when (PresentationExceptionFilterPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine(
                PresentationControlDiagnosticsPolicy.FormatSendFailureMessage(
                    operation: "target",
                    command,
                    target: target.Handle,
                    exceptionType: ex.GetType().Name,
                    message: ex.Message));
            return false;
        }
    }

    private bool TrySendToTargetCore(PresentationTarget target, PresentationCommand command, PresentationControlOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!IsTargetHandleValid(target))
        {
            return false;
        }
        var targetType = _planner.Classifier.Classify(target.Info);
        if (targetType == PresentationType.Wps && !options.AllowWps)
        {
            return false;
        }
        if (targetType == PresentationType.Office && !options.AllowOffice)
        {
            return false;
        }
        if (targetType is PresentationType.None or PresentationType.Other)
        {
            return false;
        }

        if (!options.LockStrategyWhenDegraded)
        {
            if (targetType == PresentationType.Wps)
            {
                _wpsAutoForceMessage = false;
            }
            else if (targetType == PresentationType.Office)
            {
                _officeAutoForceMessage = false;
            }
        }

        var strategy = options.Strategy;
        if (targetType == PresentationType.Wps
            && options.LockStrategyWhenDegraded
            && _wpsAutoForceMessage
            && strategy != InputStrategy.Message)
        {
            strategy = InputStrategy.Message;
        }
        if (targetType == PresentationType.Office
            && options.LockStrategyWhenDegraded
            && _officeAutoForceMessage
            && strategy != InputStrategy.Message)
        {
            strategy = InputStrategy.Message;
        }

        var sent = TrySendWithStrategy(target, command, options, strategy, out var effectiveType);
        if (!sent && strategy != InputStrategy.Message)
        {
            if (effectiveType == PresentationType.Wps)
            {
                if (options.LockStrategyWhenDegraded)
                {
                    _wpsAutoForceMessage = true;
                }
                sent = TrySendWithStrategy(target, command, options, InputStrategy.Message, out _);
            }
            else if (effectiveType == PresentationType.Office)
            {
                if (options.LockStrategyWhenDegraded)
                {
                    _officeAutoForceMessage = true;
                }
                sent = TrySendWithStrategy(target, command, options, InputStrategy.Message, out _);
            }
        }
        return sent;
    }

    private bool IsTargetHandleValid(PresentationTarget target)
    {
        return target.IsValid && _validator.IsWindowValid(target.Handle);
    }

    private bool TrySendWithStrategy(
        PresentationTarget target,
        PresentationCommand command,
        PresentationControlOptions options,
        InputStrategy strategy,
        out PresentationType targetType)
    {
        if (target.Handle == IntPtr.Zero)
        {
            targetType = PresentationType.None;
            return false;
        }
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
        if (plan.TargetType == PresentationType.Wps && IsWpsDebounced(command, target.Handle, options.WpsDebounceMs))
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
            var messageCompatibleStrategy = plan.Strategy == InputStrategy.Raw
                ? InputStrategy.Message
                : plan.Strategy;
            var fallbackBinding = _mapper.Map(plan.TargetType, command);
            var keySent = _inputSender.SendKey(
                target.Handle,
                fallbackBinding.Key,
                fallbackBinding.Modifiers,
                messageCompatibleStrategy,
                keyDownOnly: true);
            if (keySent)
            {
                RememberWpsCommand(command, target.Handle);
            }
            return keySent;
        }
        var binding = _mapper.Map(plan.TargetType, command);
        var sent = _inputSender.SendKey(target.Handle, binding.Key, binding.Modifiers, plan.Strategy, keyDownOnly);
        if (sent)
        {
            if (plan.TargetType == PresentationType.Wps)
            {
                RememberWpsCommand(command, target.Handle);
            }
        }
        return sent;
    }

    private bool IsWpsDebounced(PresentationCommand command, IntPtr target, int debounceMs)
    {
        if (target == IntPtr.Zero || _lastWpsTarget != target)
        {
            return false;
        }
        if (_lastWpsCommandType != command)
        {
            return false;
        }
        var thresholdMs = Math.Max(0, debounceMs);
        if (thresholdMs == 0)
        {
            return false;
        }
        var elapsedMs = (Stopwatch.GetTimestamp() - _lastWpsCommandTick)
            * 1000.0 / Stopwatch.Frequency;
        return elapsedMs < thresholdMs;
    }

    private void RememberWpsCommand(PresentationCommand command, IntPtr target)
    {
        _lastWpsCommandTick = Stopwatch.GetTimestamp();
        _lastWpsCommandType = command;
        _lastWpsTarget = target;
    }

    private static bool RequiresForeground(PresentationType targetType)
    {
        return targetType == PresentationType.Wps || targetType == PresentationType.Office;
    }

    private bool TryEnsureForeground(IntPtr target)
    {
        var isForeground = _foregroundController.IsForeground(target);
        if (isForeground)
        {
            return true;
        }
        _foregroundController.EnsureForeground(target);
        return _foregroundController.IsForeground(target);
    }
}
