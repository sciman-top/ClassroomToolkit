using ClassroomToolkit.Application.Abstractions;

namespace ClassroomToolkit.Application.UseCases.Presentation;

public sealed class SendPresentationCommandUseCase
{
    private readonly IPresentationGateway _gateway;

    public SendPresentationCommandUseCase(IPresentationGateway gateway)
    {
        _gateway = gateway;
    }

    public bool ExecuteForeground(PresentationCommand command, PresentationControlOptions options)
    {
        return _gateway.TrySendForeground(command, options);
    }

    public bool ExecuteToTarget(PresentationTarget target, PresentationCommand command, PresentationControlOptions options)
    {
        return _gateway.TrySendToTarget(target, command, options);
    }
}
