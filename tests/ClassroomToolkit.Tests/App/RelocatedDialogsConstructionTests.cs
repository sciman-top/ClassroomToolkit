using AwesomeAssertions;

namespace ClassroomToolkit.Tests.App;

/// <summary>
/// 功能归位轮(2026-10-25)后，这些对话框的 XAML 已随功能目录迁移。
/// WPF 的 XAML 资源解析发生在运行时而非编译期，构造一遍以锁定
/// "移动后的 BAML 加载 + x:Class 解析 + 主题资源查找"仍然成立。
/// </summary>
[Collection("WPF UI")]
public sealed class RelocatedDialogsConstructionTests
{
    [Fact]
    public void RelocatedRollCallDialogs_ShouldConstructAndClose()
    {
        WpfStaTestRunner.Run(() =>
        {
            WpfStaTestRunner.EnsureApplication();

            var classSelect = new ClassroomToolkit.App.RollCall.ClassSelectDialog(["一班", "二班"], "一班");
            classSelect.Close();

            var remoteKey = new ClassroomToolkit.App.RollCall.RemoteKeyDialog("F5");
            remoteKey.Close();

            var studentList = new ClassroomToolkit.App.RollCall.StudentListDialog(
                [new ClassroomToolkit.App.RollCall.StudentListItem("01", "张三", 1, Called: false)]);
            studentList.Close();

            var timerSet = new ClassroomToolkit.App.RollCall.TimerSetDialog(5, 0);
            timerSet.Close();

            var rollCallSettings = new ClassroomToolkit.App.RollCall.RollCallSettingsDialog(
                new ClassroomToolkit.App.Settings.AppSettings(), ["一班"]);
            rollCallSettings.Close();
        });
    }

    [Fact]
    public void RelocatedLauncherDialog_ShouldConstructAndClose()
    {
        WpfStaTestRunner.Run(() =>
        {
            WpfStaTestRunner.EnsureApplication();

            var autoExit = new ClassroomToolkit.App.Launcher.AutoExitDialog(5);
            autoExit.Close();
        });
    }

    [Fact]
    public void RelocatedWindows_ShouldConstructAndClose()
    {
        WpfStaTestRunner.Run(() =>
        {
            WpfStaTestRunner.EnsureApplication();

            var paintSettings = new ClassroomToolkit.App.Paint.PaintSettingsDialog(
                new ClassroomToolkit.App.Settings.AppSettings());
            paintSettings.Close();

            var groupOverlay = new ClassroomToolkit.App.RollCall.RollCallGroupOverlayWindow();
            groupOverlay.Close();

            var launcherBubble = new ClassroomToolkit.App.Launcher.LauncherBubbleWindow();
            launcherBubble.Close();
        });
    }
}
