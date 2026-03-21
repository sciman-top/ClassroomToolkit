using System.Windows.Threading;
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class WpsHookDispatchPriorityPolicyTests
{
    [Theory]
    [InlineData("keyboard")]
    [InlineData("Keyboard")]
    public void Resolve_ShouldReturnInput_ForKeyboardSource(string source)
    {
        var priority = WpsHookDispatchPriorityPolicy.Resolve(source);

        priority.Should().Be(DispatcherPriority.Input);
    }

    [Theory]
    [InlineData("wheel")]
    [InlineData("mouse")]
    [InlineData(null)]
    public void Resolve_ShouldReturnBackground_ForNonKeyboardSource(string? source)
    {
        var priority = WpsHookDispatchPriorityPolicy.Resolve(source);

        priority.Should().Be(DispatcherPriority.Background);
    }
}
