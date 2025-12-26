using ClassroomToolkit.Interop.Presentation;
using ClassroomToolkit.Services.Presentation;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PresentationCommandMapperTests
{
    [Fact]
    public void MapNext_ShouldReturnPageDown()
    {
        var mapper = new PresentationCommandMapper();
        var binding = mapper.Map(PresentationType.Wps, PresentationCommand.Next);

        binding.Key.Should().Be(VirtualKey.PageDown);
        binding.Modifiers.Should().Be(KeyModifiers.None);
    }

    [Fact]
    public void MapPrevious_ShouldReturnPageUp()
    {
        var mapper = new PresentationCommandMapper();
        var binding = mapper.Map(PresentationType.Office, PresentationCommand.Previous);

        binding.Key.Should().Be(VirtualKey.PageUp);
        binding.Modifiers.Should().Be(KeyModifiers.None);
    }
}
