using WpfWindow = System.Windows.Window;

namespace ClassroomToolkit.App.Paint;

internal interface IWpsHookUnavailableNotifier
{
    void Notify(WpfWindow fallbackOwner);
}

internal sealed class MessageBoxWpsHookUnavailableNotifier : IWpsHookUnavailableNotifier
{
    private const string Message = "检测到 WPS 放映全局钩子不可用，已自动切换为消息投递模式。";
    private const string Title = "提示";

    public void Notify(WpfWindow fallbackOwner)
    {
        var owner = System.Windows.Application.Current?.MainWindow;
        System.Windows.MessageBox.Show(owner ?? fallbackOwner, Message, Title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }
}
