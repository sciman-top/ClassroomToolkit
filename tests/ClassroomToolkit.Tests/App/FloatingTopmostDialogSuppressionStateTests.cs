using ClassroomToolkit.App.Windowing;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingTopmostDialogSuppressionStateTests
{
    [Fact]
    public void Enter_ShouldSetSuppressed_AndRestoreAfterDispose()
    {
        FloatingTopmostDialogSuppressionState.IsSuppressed.Should().BeFalse();

        using (FloatingTopmostDialogSuppressionState.Enter())
        {
            FloatingTopmostDialogSuppressionState.IsSuppressed.Should().BeTrue();
        }

        FloatingTopmostDialogSuppressionState.IsSuppressed.Should().BeFalse();
    }

    [Fact]
    public void Enter_ShouldSupportNestedScopes()
    {
        using var outer = FloatingTopmostDialogSuppressionState.Enter();
        using var inner = FloatingTopmostDialogSuppressionState.Enter();

        FloatingTopmostDialogSuppressionState.IsSuppressed.Should().BeTrue();

        inner.Dispose();
        FloatingTopmostDialogSuppressionState.IsSuppressed.Should().BeTrue();

        outer.Dispose();
        FloatingTopmostDialogSuppressionState.IsSuppressed.Should().BeFalse();
    }
}
