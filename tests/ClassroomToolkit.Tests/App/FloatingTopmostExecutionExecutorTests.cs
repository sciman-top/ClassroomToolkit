using System.Collections.Generic;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingTopmostExecutionExecutorTests
{
    [Fact]
    public void Apply_ShouldThrow_WhenDelegateIsNull()
    {
        var plan = new FloatingTopmostExecutionPlan(
            ToolbarTopmost: true,
            RollCallTopmost: true,
            LauncherTopmost: true,
            ImageManagerTopmost: true,
            EnforceZOrder: false);

        Action act = () => FloatingTopmostExecutionExecutor.Apply(
            plan,
            toolbarWindow: null,
            rollCallWindow: null,
            launcherWindow: null,
            imageManagerWindow: null,
            applyTopmostNoActivate: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Apply_ShouldDispatchAllWindows_WithPlanFlags()
    {
        var plan = new FloatingTopmostExecutionPlan(
            ToolbarTopmost: true,
            RollCallTopmost: false,
            LauncherTopmost: true,
            ImageManagerTopmost: false,
            EnforceZOrder: true);

        var calls = new List<(bool Enabled, bool Enforce)>();
        FloatingTopmostExecutionExecutor.Apply(
            plan,
            toolbarWindow: null,
            rollCallWindow: null,
            launcherWindow: null,
            imageManagerWindow: null,
            (window, enabled, enforceZOrder) =>
            {
                calls.Add((enabled, enforceZOrder));
            });

        calls.Should().Equal(
            (true, true),
            (false, true),
            (true, true),
            (false, true));
    }
}
