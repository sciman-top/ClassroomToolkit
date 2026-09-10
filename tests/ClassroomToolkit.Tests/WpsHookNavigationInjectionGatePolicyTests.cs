using ClassroomToolkit.App.Paint;
using AwesomeAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class WpsHookNavigationInjectionGatePolicyTests
{
    [Fact]
    public void ShouldSuppressInjection_ShouldReturnTrue_WhenTargetIsForeground()
    {
        var suppressed = WpsHookNavigationInjectionGatePolicy.ShouldSuppressInjection(
            targetIsForeground: true,
            foregroundOwnedByCurrentProcess: false,
            wheelSource: false,
            wheelAsKeyEnabled: false);

        // 真实按键已直达前台放映窗，再注入必然双翻页。
        suppressed.Should().BeTrue();
    }

    [Fact]
    public void ShouldSuppressInjection_ShouldReturnTrue_WhenForeignAppIsForeground()
    {
        var suppressed = WpsHookNavigationInjectionGatePolicy.ShouldSuppressInjection(
            targetIsForeground: false,
            foregroundOwnedByCurrentProcess: false,
            wheelSource: false,
            wheelAsKeyEnabled: false);

        // 外来应用前台的按键属于该应用，不得转译为放映翻页。
        suppressed.Should().BeTrue();
    }

    [Fact]
    public void ShouldSuppressInjection_ShouldReturnFalse_WhenOwnProcessIsForeground()
    {
        var suppressed = WpsHookNavigationInjectionGatePolicy.ShouldSuppressInjection(
            targetIsForeground: false,
            foregroundOwnedByCurrentProcess: true,
            wheelSource: false,
            wheelAsKeyEnabled: false);

        // 覆盖层/工具条持有前台时保留翻页笔中继。
        suppressed.Should().BeFalse();
    }

    [Fact]
    public void ShouldSuppressInjection_ShouldReturnFalse_WhenForegroundWheelNeedsKeyMapping()
    {
        var suppressed = WpsHookNavigationInjectionGatePolicy.ShouldSuppressInjection(
            targetIsForeground: true,
            foregroundOwnedByCurrentProcess: false,
            wheelSource: true,
            wheelAsKeyEnabled: true);

        // WheelAsKey：放映端不响应原生滚轮，前台也必须注入映射按键。
        suppressed.Should().BeFalse();
    }

    [Fact]
    public void ShouldSuppressInjection_ShouldReturnTrue_WhenForegroundKeyboardEvenWithWheelAsKey()
    {
        var suppressed = WpsHookNavigationInjectionGatePolicy.ShouldSuppressInjection(
            targetIsForeground: true,
            foregroundOwnedByCurrentProcess: true,
            wheelSource: false,
            wheelAsKeyEnabled: true);

        suppressed.Should().BeTrue();
    }
}
