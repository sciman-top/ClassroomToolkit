namespace ClassroomToolkit.App.Paint;

internal static class WpsHookNavigationInjectionGatePolicy
{
    /// <summary>
    /// 决定 LL hook 收到的导航事件是否需要由本进程再注入一次翻页命令。
    /// 只有明确授权的覆盖层/工具条前台时才允许把输入中继到后台放映窗；
    /// 外部应用及本进程其他窗口都 fail-closed。
    /// </summary>
    internal static bool ShouldSuppressInjection(
        bool targetIsForeground,
        bool foregroundInputAuthorized,
        bool wheelSource,
        bool wheelAsKeyEnabled)
    {
        if (!targetIsForeground)
        {
            // 外来应用或本进程其他窗口持有前台时，输入属于该窗口（如 Word
            // 或设置对话框中的文本输入），不得转译为后台放映翻页；只有
            // 明确授权的覆盖层/工具条 HWND 才保留翻页笔中继。
            return !foregroundInputAuthorized;
        }

        // 放映窗在前台时始终保留原生输入。WheelAsKey 只用于后台目标桥接；
        // 前台再注入会与 WPS 原生滚轮形成无法确认的双翻页。
        return true;
    }
}
