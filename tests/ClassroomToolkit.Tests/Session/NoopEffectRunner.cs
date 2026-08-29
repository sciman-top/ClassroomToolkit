using ClassroomToolkit.App.Session;

namespace ClassroomToolkit.Tests.Session;

/// <summary>
/// 会话测试共用的空效果执行器：不产生任何副作用，只驱动状态机转换。
/// </summary>
internal sealed class NoopEffectRunner : IUiSessionEffectRunner
{
    public void Run(UiSessionTransition transition)
    {
    }
}
