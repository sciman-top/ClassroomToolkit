using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.Services.Presentation;

public sealed class PresentationControlPlanner
{
    private readonly PresentationClassifier _classifier;

    public PresentationControlPlanner(PresentationClassifier classifier)
    {
        _classifier = classifier;
        Classifier = classifier;
    }

    public PresentationClassifier Classifier { get; }

    public PresentationControlPlan? Plan(
        PresentationWindowInfo info,
        PresentationControlOptions options,
        PresentationCommand command)
    {
        var type = _classifier.Classify(info);
        if (type == PresentationType.Wps && !options.AllowWps)
        {
            return null;
        }
        if (type == PresentationType.Office && !options.AllowOffice)
        {
            return null;
        }
        if (type == PresentationType.None)
        {
            return null;
        }

        var strategy = ResolveStrategy(type, options.Strategy);
        var useWheel = options.WheelAsKey && type == PresentationType.Wps && IsNavigationCommand(command);
        return new PresentationControlPlan(type, strategy, useWheel);
    }

    private static InputStrategy ResolveStrategy(PresentationType type, InputStrategy requested)
    {
        if (requested != InputStrategy.Auto)
        {
            return requested;
        }
        return type switch
        {
            PresentationType.Wps => InputStrategy.Message,
            PresentationType.Office => InputStrategy.Raw,
            _ => InputStrategy.Raw
        };
    }

    private static bool IsNavigationCommand(PresentationCommand command)
    {
        return command is PresentationCommand.Next or PresentationCommand.Previous;
    }
}
