using System.Windows.Input;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.Services.Presentation;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PresentationKeyCommandPolicyTests
{
    [Theory]
    [InlineData(Key.Right, PresentationCommand.Next)]
    [InlineData(Key.Down, PresentationCommand.Next)]
    [InlineData(Key.Space, PresentationCommand.Next)]
    [InlineData(Key.Enter, PresentationCommand.Next)]
    [InlineData(Key.PageDown, PresentationCommand.Next)]
    [InlineData(Key.Left, PresentationCommand.Previous)]
    [InlineData(Key.Up, PresentationCommand.Previous)]
    [InlineData(Key.PageUp, PresentationCommand.Previous)]
    [InlineData(Key.Home, PresentationCommand.First)]
    [InlineData(Key.End, PresentationCommand.Last)]
    public void TryMap_ShouldReturnExpectedCommand(Key key, PresentationCommand expected)
    {
        var ok = PresentationKeyCommandPolicy.TryMap(key, out var command);

        ok.Should().BeTrue();
        command.Should().Be(expected);
    }

    [Fact]
    public void TryMap_ShouldReturnFalse_ForUnsupportedKey()
    {
        var ok = PresentationKeyCommandPolicy.TryMap(Key.A, out _);

        ok.Should().BeFalse();
    }
}
