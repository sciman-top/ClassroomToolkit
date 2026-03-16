using ClassroomToolkit.Domain.Models;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class StudentWorkbookTests
{
    [Fact]
    public void Constructor_ShouldFallbackToEmptyRoster_WhenInputContainsNullRoster()
    {
        var workbook = new StudentWorkbook(
            new Dictionary<string, ClassRoster>
            {
                ["高一1班"] = null!
            },
            "高一1班");

        workbook.ActiveClass.Should().Be("高一1班");
        workbook.Classes.Should().ContainKey("高一1班");
        workbook.GetActiveRoster().Students.Should().BeEmpty();
    }

    [Fact]
    public void UpdateClass_ShouldFallbackToEmptyRoster_WhenRosterIsNull()
    {
        var workbook = new StudentWorkbook(
            new Dictionary<string, ClassRoster>
            {
                ["高一1班"] = new("高一1班", Array.Empty<StudentRecord>())
            },
            "高一1班");

        workbook.UpdateClass("高一2班", null!);
        workbook.SetActiveClass("高一2班");

        workbook.Classes.Should().ContainKey("高一2班");
        workbook.GetActiveRoster().Students.Should().BeEmpty();
    }
}
