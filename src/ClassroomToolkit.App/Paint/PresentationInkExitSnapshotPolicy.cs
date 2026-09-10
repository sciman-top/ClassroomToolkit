namespace ClassroomToolkit.App.Paint;

internal static class PresentationInkExitSnapshotPolicy
{
    /// <summary>
    /// 放映批注没有逐页落盘（CacheScope=None），退出放映会清空表面；
    /// 有墨迹时必须先留一张 PNG 快照兜底，避免教师批注静默丢失。
    /// </summary>
    internal static bool ShouldCapture(bool hasDrawing, int strokeCount)
    {
        return hasDrawing && strokeCount > 0;
    }
}
