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
    public void SerializeClassState_ShouldThrowArgumentNullException_WhenStateIsNull()
    {
        Action act = () => RollStateSerializer.SerializeClassState(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SerializeWorkbookStates_ShouldThrowArgumentNullException_WhenStatesIsNull()
    {
        Action act = () => RollStateSerializer.SerializeWorkbookStates(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DeserializeInvalid_ShouldReturnNull()
    {
        var restored = RollStateSerializer.DeserializeClassState("{invalid");
        restored.Should().BeNull();
    }

    [Fact]
    public void SerializeWorkbookStates_ShouldIncludeRevisionMetadata()
    {
        var json = RollStateSerializer.SerializeWorkbookStates(new Dictionary<string, ClassRollState>());

        var found = RollStateSerializer.TryReadWorkbookMetadata(json, out var revision, out var updatedAtUtc);

        found.Should().BeTrue();
        revision.Should().NotBeNull();
        updatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void DeserializeWorkbookStates_ShouldSupportLegacyDictionaryPayload()
    {
        var legacyJson = "{\"班级1\":{\"currentGroup\":\"一组\"}}";

        var states = RollStateSerializer.DeserializeWorkbookStates(legacyJson);

        states.Should().ContainKey("班级1");
        states["班级1"].CurrentGroup.Should().Be("一组");
    }
}
