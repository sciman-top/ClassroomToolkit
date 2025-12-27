using ClassroomToolkit.Interop.Presentation;
using ClassroomToolkit.Services.Presentation;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PresentationControlPlannerTests
{
    [Fact]
    public void AutoStrategy_ShouldPreferWpsRawMode()
    {
        var classifier = new PresentationClassifier();
        var planner = new PresentationControlPlanner(classifier);
        var info = new PresentationWindowInfo(123, "wpspresentation.exe", new[] { "wpsshowframe" });
        var options = new PresentationControlOptions { Strategy = InputStrategy.Auto, WheelAsKey = true };

        var plan = planner.Plan(info, options, PresentationCommand.Next);

        plan.Should().NotBeNull();
        plan!.TargetType.Should().Be(PresentationType.Wps);
        plan.Strategy.Should().Be(InputStrategy.Raw);
        plan.UseWheelAsKey.Should().BeTrue();
    }

    [Fact]
    public void AutoStrategy_ShouldPreferOfficeRawMode()
    {
        var classifier = new PresentationClassifier();
        var planner = new PresentationControlPlanner(classifier);
        var info = new PresentationWindowInfo(456, "powerpnt.exe", new[] { "screenclass" });
        var options = new PresentationControlOptions { Strategy = InputStrategy.Auto };

        var plan = planner.Plan(info, options, PresentationCommand.Next);

        plan.Should().NotBeNull();
        plan!.TargetType.Should().Be(PresentationType.Office);
        plan.Strategy.Should().Be(InputStrategy.Raw);
    }

    [Fact]
    public void StrategyOverride_ShouldBeRespected()
    {
        var classifier = new PresentationClassifier();
        var planner = new PresentationControlPlanner(classifier);
        var info = new PresentationWindowInfo(789, "wpspresentation.exe", new[] { "wpsshowframe" });
        var options = new PresentationControlOptions { Strategy = InputStrategy.Raw };

        var plan = planner.Plan(info, options, PresentationCommand.Next);

        plan.Should().NotBeNull();
        plan!.Strategy.Should().Be(InputStrategy.Raw);
    }

    [Fact]
    public void DisabledWps_ShouldReturnNull()
    {
        var classifier = new PresentationClassifier();
        var planner = new PresentationControlPlanner(classifier);
        var info = new PresentationWindowInfo(789, "wpspresentation.exe", new[] { "wpsshowframe" });
        var options = new PresentationControlOptions { Strategy = InputStrategy.Auto, AllowWps = false };

        var plan = planner.Plan(info, options, PresentationCommand.Next);

        plan.Should().BeNull();
    }
}
