using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Domain.Utilities;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ClassRosterTests
{
    [Fact]
    public void GroupIndexMap_AllGroup_ShouldAlwaysContainAllStudentIndexes()
    {
        var students = new[]
        {
            StudentRecord.Create("001", "张三", "一班", IdentityUtils.AllGroupName),
            StudentRecord.Create("002", "李四", "一班", "一组")
        };

        var roster = new ClassRoster("一班", students);

        roster.GroupIndexMap.Should().ContainKey(IdentityUtils.AllGroupName);
        roster.GroupIndexMap[IdentityUtils.AllGroupName].Should().Equal(0, 1);
    }

    [Fact]
    public void Groups_ShouldNotDuplicateReservedAllGroup_WhenStudentGroupContainsAllGroupName()
    {
        var students = new[]
        {
            StudentRecord.Create("001", "张三", "一班", IdentityUtils.AllGroupName),
            StudentRecord.Create("002", "李四", "一班", "一组")
        };

        var roster = new ClassRoster("一班", students);

        roster.Groups.Should().Equal(IdentityUtils.AllGroupName, "一组");
    }
}
