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
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    public bool TrySendForeground(AppPresentationCommand command, AppPresentationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!TryMapCommand(command, out var mappedCommand))
        {
            return false;
        }
        return _service.TrySendForeground(mappedCommand, MapOptions(options));
    }

    public bool TrySendToTarget(AppPresentationTarget target, AppPresentationCommand command, AppPresentationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!TryMapCommand(command, out var mappedCommand))
        {
            return false;
        }
        var interopTarget = new InteropPresentationTarget(target.Handle, InteropPresentationWindowInfo.FromProcess(string.Empty));
        return _service.TrySendToTarget(interopTarget, mappedCommand, MapOptions(options));
    }

    private static bool TryMapCommand(AppPresentationCommand command, out PresentationCommand mapped)
    {
        switch (command)
        {
            case AppPresentationCommand.Next:
                mapped = PresentationCommand.Next;
                return true;
            case AppPresentationCommand.Previous:
                mapped = PresentationCommand.Previous;
                return true;
            case AppPresentationCommand.First:
                mapped = PresentationCommand.First;
                return true;
            case AppPresentationCommand.Last:
                mapped = PresentationCommand.Last;
                return true;
            case AppPresentationCommand.BlackScreenToggle:
                mapped = PresentationCommand.BlackScreenToggle;
                return true;
            case AppPresentationCommand.WhiteScreenToggle:
                mapped = PresentationCommand.WhiteScreenToggle;
                return true;
            default:
                mapped = default;
                return false;
        }
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
