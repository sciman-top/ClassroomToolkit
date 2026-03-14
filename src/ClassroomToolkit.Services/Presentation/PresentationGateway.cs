using ClassroomToolkit.Application.Abstractions;
using AppPresentationCommand = ClassroomToolkit.Application.UseCases.Presentation.PresentationCommand;
using AppPresentationOptions = ClassroomToolkit.Application.UseCases.Presentation.PresentationControlOptions;
using AppPresentationTarget = ClassroomToolkit.Application.UseCases.Presentation.PresentationTarget;
using InteropPresentationTarget = ClassroomToolkit.Interop.Presentation.PresentationTarget;
using InteropInputStrategy = ClassroomToolkit.Interop.Presentation.InputStrategy;
using InteropPresentationWindowInfo = ClassroomToolkit.Interop.Presentation.PresentationWindowInfo;

namespace ClassroomToolkit.Services.Presentation;

public sealed class PresentationGateway : IPresentationGateway
{
    private readonly PresentationControlService _service;

    public PresentationGateway(PresentationControlService service)
    {
        _service = service;
    }

    public bool TrySendForeground(AppPresentationCommand command, AppPresentationOptions options)
    {
        return _service.TrySendForeground(MapCommand(command), MapOptions(options));
    }

    public bool TrySendToTarget(AppPresentationTarget target, AppPresentationCommand command, AppPresentationOptions options)
    {
        var interopTarget = new InteropPresentationTarget(target.Handle, InteropPresentationWindowInfo.FromProcess(string.Empty));
        return _service.TrySendToTarget(interopTarget, MapCommand(command), MapOptions(options));
    }

    private static PresentationCommand MapCommand(AppPresentationCommand command)
    {
        return command switch
        {
            AppPresentationCommand.Next => PresentationCommand.Next,
            AppPresentationCommand.Previous => PresentationCommand.Previous,
            AppPresentationCommand.First => PresentationCommand.First,
            AppPresentationCommand.Last => PresentationCommand.Last,
            AppPresentationCommand.BlackScreenToggle => PresentationCommand.BlackScreenToggle,
            AppPresentationCommand.WhiteScreenToggle => PresentationCommand.WhiteScreenToggle,
            _ => PresentationCommand.Next
        };
    }

    private static PresentationControlOptions MapOptions(AppPresentationOptions options)
    {
        return new PresentationControlOptions
        {
            Strategy = InteropInputStrategy.Auto,
            WheelAsKey = options.WheelAsKey,
            AllowOffice = options.AllowOffice,
            AllowWps = options.AllowWps,
            WpsDebounceMs = options.WpsDebounceMs,
            LockStrategyWhenDegraded = options.LockStrategyWhenDegraded
        };
    }
}
