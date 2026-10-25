using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using ClassroomToolkit.App.Behaviors;
using ClassroomToolkit.App.Commands;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests.App;

[Collection("WPF UI")]
public sealed class LongPressBehaviorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CommandChange_ShouldCancelPendingPress(bool removeCommand)
    {
        WpfStaTestRunner.Run(() =>
        {
            var element = new Button();
            LongPressBehavior.SetCommand(element, new RelayCommand(() => { }));
            typeof(LongPressBehavior).GetMethod("StartPressTimer", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [element]);
            var timerProperty = GetProperty("TimerProperty");
            element.GetValue(timerProperty).Should().NotBeNull();

            LongPressBehavior.SetCommand(element, removeCommand ? null : new RelayCommand(() => { }));

            element.GetValue(timerProperty).Should().BeNull("the previous gesture must not invoke a replacement command or retain its element");
        });
    }

    private static DependencyProperty GetProperty(string name) =>
        (DependencyProperty)typeof(LongPressBehavior).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
}
