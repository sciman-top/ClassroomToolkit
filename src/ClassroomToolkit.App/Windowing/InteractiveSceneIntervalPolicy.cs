namespace ClassroomToolkit.App.Windowing;

/// <summary>
/// 浮层交互场景（ Overlay 可见且处于图片/白板模式）判定与间隔选择的单一公式源。
/// 各 retouch/dedup 策略此前各自内联同一份三元表达式，现统一委托到此。
/// </summary>
internal static class InteractiveSceneIntervalPolicy
{
    internal static bool IsInteractiveScene(bool overlayVisible, bool photoModeActive, bool whiteboardActive)
    {
        return overlayVisible && (photoModeActive || whiteboardActive);
    }

    internal static int ResolveMs(
        bool overlayVisible,
        bool photoModeActive,
        bool whiteboardActive,
        int defaultMs,
        int interactiveMs)
    {
        return IsInteractiveScene(overlayVisible, photoModeActive, whiteboardActive)
            ? interactiveMs
            : defaultMs;
    }
}
