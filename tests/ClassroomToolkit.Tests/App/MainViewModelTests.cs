using ClassroomToolkit.App.ViewModels;
using AwesomeAssertions;
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void VisibilityChange_ShouldNotifyStateAndLabel_OnlyWhenValueChanges(bool paint)
    {
        var vm = new MainViewModel();
        var notifications = new List<string?>();
        vm.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        if (paint)
        {
            vm.IsPaintActive = true;
            vm.IsPaintActive = true;
            vm.IsPaintActive = false;
        }
        else
        {
            vm.IsRollCallVisible = true;
            vm.IsRollCallVisible = true;
            vm.IsRollCallVisible = false;
        }

        var stateProperty = paint ? nameof(vm.IsPaintActive) : nameof(vm.IsRollCallVisible);
        var labelProperty = paint ? nameof(vm.PaintButtonText) : nameof(vm.RollCallButtonText);
        notifications.Should().Equal(stateProperty, labelProperty, stateProperty, labelProperty);
    }
}
