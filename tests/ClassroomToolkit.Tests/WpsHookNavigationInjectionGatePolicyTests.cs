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
            foregroundInputAuthorized: false,
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
            foregroundInputAuthorized: false,
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
            foregroundInputAuthorized: true,
            wheelSource: false,
            wheelAsKeyEnabled: false);

        // 覆盖层/工具条持有前台时保留翻页笔中继。
        suppressed.Should().BeFalse();
    }

    [Fact]
    public void ShouldSuppressInjection_ShouldReturnTrue_WhenForegroundWheelMappingWouldDuplicateNativeInput()
    {
        var suppressed = WpsHookNavigationInjectionGatePolicy.ShouldSuppressInjection(
            targetIsForeground: true,
            foregroundInputAuthorized: false,
            wheelSource: true,
            wheelAsKeyEnabled: true);

        // WheelAsKey 仅桥接后台目标；WPS 已前台时保留原生滚轮，避免双翻页。
        suppressed.Should().BeTrue();
    }

    [Fact]
    public void ShouldSuppressInjection_ShouldReturnTrue_WhenForegroundKeyboardEvenWithWheelAsKey()
    {
        var suppressed = WpsHookNavigationInjectionGatePolicy.ShouldSuppressInjection(
            targetIsForeground: true,
            foregroundInputAuthorized: true,
            wheelSource: false,
            wheelAsKeyEnabled: true);

        suppressed.Should().BeTrue();
    }
}
