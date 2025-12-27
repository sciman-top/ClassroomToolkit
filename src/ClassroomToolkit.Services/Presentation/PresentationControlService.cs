using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.Services.Presentation;

public sealed class PresentationControlService
{
    private readonly PresentationControlPlanner _planner;
    private readonly PresentationCommandMapper _mapper;
    private readonly IInputSender _inputSender;
    private readonly Win32PresentationResolver _resolver;
    private DateTime _lastWpsCommand = DateTime.MinValue;
    private PresentationTarget _lastTarget = PresentationTarget.Empty;
    private readonly uint _currentProcessId;

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
        var plan = _planner.Plan(target.Info, options, command);
        if (plan == null)
        {
            return false;
        }
        if (plan.TargetType == PresentationType.Wps && IsWpsDebounced())
        {
            return false;
        }
        var keyDownOnly = plan.TargetType == PresentationType.Wps;
        if (plan.TargetType == PresentationType.Wps
            && !plan.UseWheelAsKey
            && command is PresentationCommand.Next or PresentationCommand.Previous)
        {
            var delta = command == PresentationCommand.Next ? -120 : 120;
            var wheelSent = _inputSender.SendWheel(target.Handle, delta, plan.Strategy);
            if (wheelSent)
            {
                _lastTarget = target;
                _lastWpsCommand = DateTime.UtcNow;
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
                _lastWpsCommand = DateTime.UtcNow;
            }
        }
        return sent;
    }

    private bool IsWpsDebounced()
    {
        var elapsed = DateTime.UtcNow - _lastWpsCommand;
        return elapsed.TotalMilliseconds < 200;
    }
}
