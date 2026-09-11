namespace ClassroomToolkit.App.Paint;

internal static class PresentationInkExitSnapshotPolicy
{
    /// <summary>
    /// 放映批注没有逐页落盘（CacheScope=None），退出放映会清空表面；
    /// 有墨迹时必须先留一张 PNG 快照兜底，避免教师批注静默丢失。
    /// 只看位图表面是否有墨迹：出厂默认 ink_record_enabled=false 时
    /// 笔画不进入向量表（strokeCount 恒为 0），不能作为判据。
    /// </summary>
    internal static bool ShouldCapture(bool hasDrawing)
    {
        return hasDrawing;
    }
}
