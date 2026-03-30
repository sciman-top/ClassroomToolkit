using ClassroomToolkit.Application.UseCases.Presentation;

namespace ClassroomToolkit.Application.Abstractions;

public interface IPresentationGateway
{
    bool TrySendForeground(PresentationCommand command, PresentationControlOptions options);
    bool TrySendToTarget(PresentationTarget target, PresentationCommand command, PresentationControlOptions options);
}
