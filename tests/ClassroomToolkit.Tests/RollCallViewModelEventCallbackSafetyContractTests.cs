using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class RollCallViewModelEventCallbackSafetyContractTests
{
    [Fact]
    public void TimerAndReminderCallbacks_ShouldBeGuardedBySafeActionExecutor()
    {
        var source = File.ReadAllText(GetViewModelSourcePath());

        source.Should().Contain("SafeActionExecutionExecutor.TryExecute(");
        source.Should().Contain("TimerCompleted?.Invoke()");
        source.Should().Contain("ReminderTriggered?.Invoke()");
        source.Should().Contain("timer completed callback failed");
        source.Should().Contain("reminder callback failed");
    }

    [Fact]
    public void DataLoadFailedCallback_ShouldBeGuardedBySafeActionExecutor()
    {
        var source = File.ReadAllText(GetViewModelDataSourcePath());

        source.Should().Contain("SafeActionExecutionExecutor.TryExecute(");
        source.Should().Contain("DataLoadFailed?.Invoke(result.ErrorMessage)");
        source.Should().Contain("data load failed callback failed");
    }

    [Fact]
    public void DataSaveFailedCallback_ShouldBeGuardedBySafeActionExecutor()
    {
        var source = File.ReadAllText(GetViewModelNavigationSourcePath());

        source.Should().Contain("SafeActionExecutionExecutor.TryExecute(");
        source.Should().Contain("DataSaveFailed?.Invoke($\"保存状态失败: {ex.Message}\")");
        source.Should().Contain("data save failed callback failed");
    }

    private static string GetViewModelSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "ViewModels",
            "RollCallViewModel.cs");
    }

    private static string GetViewModelDataSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "ViewModels",
            "RollCallViewModel.Data.cs");
    }

    private static string GetViewModelNavigationSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "ViewModels",
            "RollCallViewModel.Navigation.cs");
    }
}
