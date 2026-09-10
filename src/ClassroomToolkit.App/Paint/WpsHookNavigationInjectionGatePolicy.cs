namespace ClassroomToolkit.App.Paint;

internal static class WpsHookNavigationInjectionGatePolicy
{
    /// <summary>
    /// 决定 LL hook 收到的导航事件是否需要由本进程再注入一次翻页命令。
    /// hook 从不吞键（blockOnly 恒为 false）：真实按键/滚轮会原样送达前台窗口。
    /// </summary>
    internal static bool ShouldSuppressInjection(
        bool targetIsForeground,
        bool foregroundOwnedByCurrentProcess,
        bool wheelSource,
        bool wheelAsKeyEnabled)
    {
        if (!targetIsForeground)
        {
            // 外来应用持有前台时，按键属于该应用（如 Word 里打空格），
            // 不得被转译为后台放映翻页；仅本进程前台（覆盖层/工具条）时保留
            // 翻页笔中继。
            return !foregroundOwnedByCurrentProcess;
        }

        // 放映窗在前台：键盘/滚轮原生直达。仅 WheelAsKey（放映端不响应原生
        // 滚轮、需映射为按键）时前台滚轮仍必须注入。
        if (wheelSource && wheelAsKeyEnabled)
        {
            return false;
        }

        return true;
    }
}
