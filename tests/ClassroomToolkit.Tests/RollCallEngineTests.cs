using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Domain.Services;
using ClassroomToolkit.Domain.Utilities;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class RollCallEngineTests
{
    [Fact]
    public void RollNext_ShouldNotRepeatWithinGroup()
    {
        var students = new List<StudentRecord>
        {
            StudentRecord.Create("1", "张三", "一班", "一组"),
            StudentRecord.Create("2", "李四", "一班", "一组"),
            StudentRecord.Create("3", "王五", "一班", "二组"),
        };
        var roster = new ClassRoster("一班", students);
        var engine = new RollCallEngine(roster);

        engine.SetCurrentGroup("一组");
        var first = engine.RollNext();
        var second = engine.RollNext();

        first.Should().NotBeNull();
        second.Should().NotBeNull();
        first!.StudentId.Should().NotBe(second!.StudentId);
    }

    [Fact]
    public void ResetGroup_ShouldAllowRedraw()
    {
        var students = new List<StudentRecord>
        {
            StudentRecord.Create("1", "张三", "一班", "一组"),
            StudentRecord.Create("2", "李四", "一班", "一组"),
        };
        var roster = new ClassRoster("一班", students);
        var engine = new RollCallEngine(roster);

        engine.SetCurrentGroup("一组");
        var first = engine.RollNext();
        var second = engine.RollNext();

        engine.ResetGroup("一组");
        var redraw = engine.RollNext();

        first.Should().NotBeNull();
        second.Should().NotBeNull();
        redraw.Should().NotBeNull();
        redraw!.StudentId.Should().BeOneOf(first!.StudentId, second!.StudentId);
    }

    [Fact]
    public void SetCurrentGroup_ShouldFallbackToAll()
    {
        var roster = new ClassRoster("一班", Array.Empty<StudentRecord>());
        var engine = new RollCallEngine(roster);

        engine.SetCurrentGroup("不存在的组");

        engine.CurrentGroup.Should().Be(IdentityUtils.AllGroupName);
    }
}
