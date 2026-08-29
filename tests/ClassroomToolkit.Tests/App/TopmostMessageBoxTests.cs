using System.Windows;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

[Collection("WPF UI")]
public sealed class TopmostMessageBoxTests
{
    [Fact]
    public void ExecuteWithOwnerTopmostSuppressed_ShouldLowerOwnerAndRestoreAfterAction()
    {
        WpfStaTestRunner.Run(() =>
        {
            var owner = new Window { Topmost = true };

            var result = TopmostMessageBox.ExecuteWithOwnerTopmostSuppressed(owner, () =>
            {
                FloatingTopmostDialogSuppressionState.IsSuppressed.Should().BeTrue();
                owner.Topmost.Should().BeFalse();
                return MessageBoxResult.OK;
            });

            result.Should().Be(MessageBoxResult.OK);
            owner.Topmost.Should().BeTrue();
            FloatingTopmostDialogSuppressionState.IsSuppressed.Should().BeFalse();
        });
    }

    [Fact]
    public void ExecuteWithOwnerTopmostSuppressed_ShouldRestoreOwnerWhenActionThrows()
    {
        WpfStaTestRunner.Run(() =>
        {
            var owner = new Window { Topmost = true };

            Action action = () => TopmostMessageBox.ExecuteWithOwnerTopmostSuppressed<MessageBoxResult>(
                owner,
                () => throw new InvalidOperationException("expected"));

            action.Should().Throw<InvalidOperationException>();
            owner.Topmost.Should().BeTrue();
            FloatingTopmostDialogSuppressionState.IsSuppressed.Should().BeFalse();
        });
    }
}
