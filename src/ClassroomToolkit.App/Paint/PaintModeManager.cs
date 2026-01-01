using System;

namespace ClassroomToolkit.App.Paint;

/// <summary>
/// 管理全局绘图模式状态的单例类
/// 用于协调绘图覆盖窗口、工具条窗口和点名窗口之间的穿透行为
/// </summary>
public sealed class PaintModeManager
{
    private static readonly Lazy<PaintModeManager> _instance = new(() => new PaintModeManager());
    
    private bool _isPaintMode;
    private bool _isDrawing;
    
    /// <summary>
    /// 获取 PaintModeManager 的单例实例
    /// </summary>
    public static PaintModeManager Instance => _instance.Value;
    
    private PaintModeManager()
    {
    }
    
    /// <summary>
    /// 获取或设置当前是否处于绘图模式（画笔、橡皮擦、形状等）
    /// </summary>
    public bool IsPaintMode
    {
        get => _isPaintMode;
        set
        {
            if (_isPaintMode != value)
            {
                _isPaintMode = value;
                PaintModeChanged?.Invoke(value);
            }
        }
    }
    
    /// <summary>
    /// 获取或设置当前是否正在绘图（鼠标左键按下书写状态）
    /// </summary>
    public bool IsDrawing
    {
        get => _isDrawing;
        set
        {
            if (_isDrawing != value)
            {
                _isDrawing = value;
                IsDrawingChanged?.Invoke(value);
            }
        }
    }
    
    /// <summary>
    /// 当绘图模式状态改变时触发
    /// </summary>
    public event Action<bool>? PaintModeChanged;
    
    /// <summary>
    /// 当绘图状态改变时触发（开始/结束书写）
    /// </summary>
    public event Action<bool>? IsDrawingChanged;
    
    /// <summary>
    /// 判断是否应该允许窗口穿透
    /// 工具条窗口：永远不穿透，确保用户始终可以点击按钮
    /// 点名窗口：根据绘图状态决定是否穿透
    /// </summary>
    /// <param name="isToolbar">是否是工具条窗口</param>
    /// <returns>true 表示应该穿透，false 表示不穿透</returns>
    public bool ShouldAllowTransparency(bool isToolbar)
    {
        // 工具条窗口永远不穿透
        if (isToolbar)
        {
            return false;
        }
        
        // 点名窗口：只有在绘图模式且正在绘图时才穿透
        return _isPaintMode && _isDrawing;
    }
    
    /// <summary>
    /// 判断工具条窗口是否应该允许穿透
    /// 工具条窗口的穿透策略：只有在正在绘图时才穿透
    /// </summary>
    /// <returns>true 表示应该穿透，false 表示不穿透</returns>
    public bool ShouldToolbarAllowTransparency()
    {
        // 只有在正在绘图时才穿透
        return _isPaintMode && _isDrawing;
    }
}
