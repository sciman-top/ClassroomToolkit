using System.Diagnostics.CodeAnalysis;

namespace ClassroomToolkit.Interop.Presentation;

/// <summary>
/// GlobalHookService 持有的键盘钩子句柄接缝。以接口暴露 StartAsync/IsActive/
/// 事件与释放，使注册-回滚契约可以用假句柄做行为级验证，而不必真实安装
/// WH_KEYBOARD_LL 钩子。
/// </summary>
public interface IKeyboardHookHandle : IDisposable
{
    [SuppressMessage("Design", "CA1003:Use generic event handler instances", Justification = "Action-based event is part of the existing hook adapter contract.")]
    event Action<KeyBinding>? BindingTriggered;

    Task StartAsync();

    bool IsActive { get; }
}
