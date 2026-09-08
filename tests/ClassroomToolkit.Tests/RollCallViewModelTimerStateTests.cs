using ClassroomToolkit.App.ViewModels;
using ClassroomToolkit.Application.UseCases.RollCall;
using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Domain.Timers;
using ClassroomToolkit.Infra.Storage;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests;

public sealed class RollCallViewModelTimerStateTests
{
    [Fact]
    public void ApplyTimerState_ShouldClampSettingsValues_InsteadOfOverflowing()
    {
        // 回归：设置文件被手工编辑出超大值（timer_countdown_minutes=2000000000）时，
        // 旧实现 minutes * 60 + seconds 在 int 域溢出回绕为负，倒计时静默归零且无法启动。
        var tempRoot = Path.Combine(Path.GetTempPath(), $"ctoolkit-timer-state-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var workbookPath = Path.Combine(tempRoot, "students.xlsx");
        File.WriteAllText(workbookPath, "stub");
        try
        {
            var useCase = new RollCallWorkbookUseCase(new RollCallWorkbookStoreAdapter());
            using var viewModel = new RollCallViewModel(workbookPath, useCase);

            var act = () => viewModel.ApplyTimerState(
                isRollCallMode: false,
                TimerMode.Countdown,
                minutes: 2_000_000_000,
                seconds: 59,
                secondsLeft: 9059,
                stopwatchSeconds: 0,
                running: false);

            act.Should().NotThrow();
            // 钳制后 total = 150*60+59；旧实现溢出为负会被引擎钳 0，导致倒计时静默归零。
            viewModel.TimerSecondsLeft.Should().Be(9059);
        }
        finally
        {
            if (File.Exists(workbookPath))
            {
                File.Delete(workbookPath);
            }
            Directory.Delete(tempRoot);
        }
    }
}
