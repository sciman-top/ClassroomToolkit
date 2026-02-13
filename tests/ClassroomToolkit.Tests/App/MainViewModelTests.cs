using ClassroomToolkit.App.ViewModels;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class MainViewModelTests
{
    [Fact]
    public void PaintButtonText_ShouldDefaultToPaint_WhenInactive()
    {
        var vm = new MainViewModel();

        vm.PaintButtonText.Should().Be("画笔");
    }

    [Fact]
    public void PaintButtonText_ShouldSwitch_WhenIsPaintActiveChanges()
    {
        var vm = new MainViewModel();

        vm.IsPaintActive = true;

        vm.PaintButtonText.Should().Be("隐藏画笔");
    }

    [Fact]
    public void RollCallButtonText_ShouldSwitch_WhenIsRollCallVisibleChanges()
    {
        var vm = new MainViewModel();

        vm.IsRollCallVisible = true;

        vm.RollCallButtonText.Should().Be("隐藏点名");
    }
}
