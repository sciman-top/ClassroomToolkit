using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.Services.Presentation;

public sealed class PresentationControlPlanner
{
    public PresentationControlPlanner(PresentationClassifier classifier)
    {
        ArgumentNullException.ThrowIfNull(classifier);
        Classifier = classifier;
    }

    public PresentationClassifier Classifier { get; }

    public PresentationControlPlan? Plan(
        PresentationWindowInfo info,
        PresentationControlOptions options,
        PresentationCommand command)
    {
        var type = Classifier.Classify(info);
        if (type == PresentationType.Wps && !options.AllowWps)
        {
            return null;
        }
        if (type == PresentationType.Office && !options.AllowOffice)
        {
            return null;
        }
        if (type is PresentationType.None or PresentationType.Other)
        {
            return null;
        }

        var strategy = ResolveStrategy(options.Strategy);
        var useWheel = options.WheelAsKey && type == PresentationType.Wps && IsNavigationCommand(command);
        return new PresentationControlPlan(type, strategy, useWheel);
    }

    private static InputStrategy ResolveStrategy(InputStrategy requested)
    {
        if (requested != InputStrategy.Auto)
        {
            return requested;
        }
        return InputStrategy.Raw;
    }

    private static bool IsNavigationCommand(PresentationCommand command)
    {
        return command is PresentationCommand.Next or PresentationCommand.Previous;
    }
}
