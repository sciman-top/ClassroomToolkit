using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.Services.Presentation;

public sealed class PresentationControlService
{
    private readonly PresentationControlPlanner _planner;
    private readonly PresentationCommandMapper _mapper;
    private readonly IInputSender _inputSender;
    private readonly Win32PresentationResolver _resolver;
    private DateTime _lastWpsCommand = DateTime.MinValue;

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
    }

    public bool TrySendForeground(PresentationCommand command, PresentationControlOptions options)
    {
        var target = _resolver.ResolveForeground();
        if (!target.IsValid)
        {
            return false;
        }
        return TrySendToTarget(target, command, options);
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
        var binding = _mapper.Map(plan.TargetType, command);
        var sent = _inputSender.SendKey(target.Handle, binding.Key, binding.Modifiers, plan.Strategy);
        if (sent && plan.TargetType == PresentationType.Wps)
        {
            _lastWpsCommand = DateTime.UtcNow;
        }
        return sent;
    }

    private bool IsWpsDebounced()
    {
        var elapsed = DateTime.UtcNow - _lastWpsCommand;
        return elapsed.TotalMilliseconds < 200;
    }
}
