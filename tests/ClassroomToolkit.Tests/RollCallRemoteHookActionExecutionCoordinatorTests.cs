using ClassroomToolkit.App.RollCall;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class RollCallRemoteHookActionExecutionCoordinatorTests
{
    [Fact]
    public void ExecuteRoll_ShouldSkip_WhenNotInRollCallMode()
    {
        var tryRollCalls = 0;
        var updateCalls = 0;
        var speakCalls = 0;
        var saveCalls = 0;
        var messages = new List<string>();

        RollCallRemoteHookActionExecutionCoordinator.ExecuteRoll(
            isRollCallMode: false,
            tryRollNext: (out string? message) =>
            {
                tryRollCalls++;
                message = string.Empty;
                return true;
            },
            updatePhotoDisplay: () => updateCalls++,
            speakStudentName: () => speakCalls++,
            scheduleRollStateSave: () => saveCalls++,
            showRollCallMessage: messages.Add);

        tryRollCalls.Should().Be(0);
        updateCalls.Should().Be(0);
        speakCalls.Should().Be(0);
        saveCalls.Should().Be(0);
        messages.Should().BeEmpty();
    }

    [Fact]
    public void ExecuteRoll_ShouldInvokeSuccessActions_WhenRollSucceeds()
    {
        var updateCalls = 0;
        var speakCalls = 0;
        var saveCalls = 0;
        var messages = new List<string>();

        RollCallRemoteHookActionExecutionCoordinator.ExecuteRoll(
            isRollCallMode: true,
            tryRollNext: (out string? message) =>
            {
                message = string.Empty;
                return true;
            },
            updatePhotoDisplay: () => updateCalls++,
            speakStudentName: () => speakCalls++,
            scheduleRollStateSave: () => saveCalls++,
            showRollCallMessage: messages.Add);

        updateCalls.Should().Be(1);
        speakCalls.Should().Be(1);
        saveCalls.Should().Be(1);
        messages.Should().BeEmpty();
    }

    [Fact]
    public void ExecuteRoll_ShouldReportMessage_WhenRollFailsWithMessage()
    {
        var updateCalls = 0;
        var speakCalls = 0;
        var saveCalls = 0;
        var messages = new List<string>();

        RollCallRemoteHookActionExecutionCoordinator.ExecuteRoll(
            isRollCallMode: true,
            tryRollNext: (out string? message) =>
            {
                message = "failed-message";
                return false;
            },
            updatePhotoDisplay: () => updateCalls++,
            speakStudentName: () => speakCalls++,
            scheduleRollStateSave: () => saveCalls++,
            showRollCallMessage: messages.Add);

        updateCalls.Should().Be(0);
        speakCalls.Should().Be(0);
        saveCalls.Should().Be(0);
        messages.Should().Equal("failed-message");
    }

    [Fact]
    public void ExecuteRoll_ShouldSuppressWhitespaceMessage_WhenRollFails()
    {
        var messages = new List<string>();

        RollCallRemoteHookActionExecutionCoordinator.ExecuteRoll(
            isRollCallMode: true,
            tryRollNext: (out string? message) =>
            {
                message = "  ";
                return false;
            },
            updatePhotoDisplay: () => { },
            speakStudentName: () => { },
            scheduleRollStateSave: () => { },
            showRollCallMessage: messages.Add);

        messages.Should().BeEmpty();
    }

    [Fact]
    public void ExecuteGroupSwitch_ShouldSkip_WhenNotInRollCallMode()
    {
        var switchCalls = 0;
        var overlayCalls = 0;
        var saveCalls = 0;

        RollCallRemoteHookActionExecutionCoordinator.ExecuteGroupSwitch(
            isRollCallMode: false,
            switchToNextGroup: () => switchCalls++,
            showGroupOverlay: () => overlayCalls++,
            scheduleRollStateSave: () => saveCalls++);

        switchCalls.Should().Be(0);
        overlayCalls.Should().Be(0);
        saveCalls.Should().Be(0);
    }

    [Fact]
    public void ExecuteGroupSwitch_ShouldInvokeActions_WhenInRollCallMode()
    {
        var switchCalls = 0;
        var overlayCalls = 0;
        var saveCalls = 0;

        RollCallRemoteHookActionExecutionCoordinator.ExecuteGroupSwitch(
            isRollCallMode: true,
            switchToNextGroup: () => switchCalls++,
            showGroupOverlay: () => overlayCalls++,
            scheduleRollStateSave: () => saveCalls++);

        switchCalls.Should().Be(1);
        overlayCalls.Should().Be(1);
        saveCalls.Should().Be(1);
    }
}

