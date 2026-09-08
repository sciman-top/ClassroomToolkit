namespace ClassroomToolkit.Infra.Logging;

/// <summary>
/// Infra 存储层降级事件的日志出口。默认仅 Debug 输出（与历史行为一致）；
/// 宿主启动时通过 <see cref="SetSink"/> 注入文件日志转发，使 Release 无调试器
/// 环境下"规范化备份失败、快照写失败"等关键降级可留痕。
/// </summary>
public static class InfraDiagnosticsLog
{
    private static volatile Action<string>? _sink;

    public static void SetSink(Action<string>? sink)
    {
        _sink = sink;
    }

    public static void Write(string message)
    {
        System.Diagnostics.Debug.WriteLine(message);
        try
        {
            _sink?.Invoke(message);
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            // 日志转发失败不得影响数据主链；致命异常按既有策略放行。
        }
    }
}
