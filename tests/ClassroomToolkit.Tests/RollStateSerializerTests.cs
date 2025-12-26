using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Domain.Serialization;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class RollStateSerializerTests
{
    [Fact]
    public void SerializeAndDeserialize_ShouldRoundTrip()
    {
        var state = new ClassRollState
        {
            CurrentGroup = "一组",
            GroupRemaining = new Dictionary<string, List<string>>
            {
                ["一组"] = new List<string> { "rk:abc" }
            },
            GroupLast = new Dictionary<string, string?>
            {
                ["一组"] = "rk:def"
            },
            GlobalDrawn = new List<string> { "rk:def" },
            CurrentStudent = "rk:def",
            PendingStudent = "rk:ghi"
        };

        var json = RollStateSerializer.SerializeClassState(state);
        var restored = RollStateSerializer.DeserializeClassState(json);

        restored.Should().NotBeNull();
        restored!.CurrentGroup.Should().Be("一组");
        restored.GlobalDrawn.Should().ContainSingle("rk:def");
        restored.GroupRemaining.Should().ContainKey("一组");
    }

    [Fact]
    public void DeserializeInvalid_ShouldReturnNull()
    {
        var restored = RollStateSerializer.DeserializeClassState("{invalid");
        restored.Should().BeNull();
    }
}
