using System.Collections.Generic;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingTopmostDriftRepairExecutorTests
{
    [Fact]
    public void Apply_ShouldThrow_WhenDelegateIsNull()
    {
        Action act = () => FloatingTopmostDriftRepairExecutor.Apply(
            new FloatingTopmostDriftRepairPlan(
                RepairToolbar: true,
                RepairRollCall: false,
                RepairLauncher: false),
            toolbarWindow: null,
            rollCallWindow: null,
            launcherWindow: null,
            enforceZOrder: false,
            applyTopmostNoActivate: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Apply_ShouldSkipAll_WhenNoRepairFlags()
    {
        var calls = new List<(bool Enabled, bool Enforce)>();
        FloatingTopmostDriftRepairExecutor.Apply(
            new FloatingTopmostDriftRepairPlan(
                RepairToolbar: false,
                RepairRollCall: false,
                RepairLauncher: false),
            toolbarWindow: null,
            rollCallWindow: null,
            launcherWindow: null,
            enforceZOrder: false,
            (window, enabled, enforceZOrder) => calls.Add((enabled, enforceZOrder)));

        calls.Should().BeEmpty();
    }

    [Fact]
    public void Apply_ShouldOnlyInvokeEnabledRepairs_WithGivenEnforceFlag()
    {
        var calls = new List<(bool Enabled, bool Enforce)>();
        FloatingTopmostDriftRepairExecutor.Apply(
            new FloatingTopmostDriftRepairPlan(
                RepairToolbar: true,
                RepairRollCall: false,
                RepairLauncher: true),
            toolbarWindow: null,
            rollCallWindow: null,
            launcherWindow: null,
            enforceZOrder: true,
            (window, enabled, enforceZOrder) => calls.Add((enabled, enforceZOrder)));

        calls.Should().Equal(
            (true, true),
            (true, true));
    }

    [Fact]
    public void Apply_ShouldContinueAndReport_WhenSingleRepairThrows()
    {
        var callCount = 0;
        var failures = new List<Exception>();

        FloatingTopmostDriftRepairExecutor.Apply(
            new FloatingTopmostDriftRepairPlan(
                RepairToolbar: true,
                RepairRollCall: true,
                RepairLauncher: false),
            toolbarWindow: null,
            rollCallWindow: null,
            launcherWindow: null,
            enforceZOrder: false,
            applyTopmostNoActivate: (_, _, _) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("boom");
                }
            },
            onFailure: ex => failures.Add(ex));

        callCount.Should().Be(2);
        failures.Should().ContainSingle();
        failures[0].Should().BeOfType<InvalidOperationException>();
        failures[0].Message.Should().Be("boom");
    }
}
