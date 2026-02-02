namespace ClassroomToolkit.Services.Presentation;

/// <summary>
/// 窗口句柄验证器接口，用于支持测试场景下的 Mock
/// </summary>
public interface IHandleValidator
{
    /// <summary>
    /// 验证窗口句柄是否有效
    /// </summary>
    bool IsValid(IntPtr handle);
}

/// <summary>
/// 默认实现，使用 Win32 API 验证真实窗口句柄
/// </summary>
public sealed class DefaultHandleValidator : IHandleValidator
{
    public static DefaultHandleValidator Instance { get; } = new();
    
    public bool IsValid(IntPtr handle)
    {
        return ClassroomToolkit.Interop.Presentation.PresentationWindowFocus.IsWindowValid(handle);
    }
}
