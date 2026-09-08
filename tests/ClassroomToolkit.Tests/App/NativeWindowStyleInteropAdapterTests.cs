using AwesomeAssertions;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.Tests;

public sealed class NativeWindowStyleInteropAdapterTests
{
    [Fact]
    public void IsCallSuccessful_ShouldSucceed_WhenReturnValueIsNonZero_EvenWithStaleLastError()
    {
        // 线程此前失败的 Win32 调用（如向已销毁窗口 PostMessage）会残留非零
        // last-error；user32 成功路径不重置它，不得据此误判失败。
        NativeWindowStyleInteropAdapter.IsCallSuccessful(0x00000001, 1400).Should().BeTrue();
        NativeWindowStyleInteropAdapter.IsCallSuccessful(unchecked((int)0x80000000), 1400).Should().BeTrue();
    }

    [Fact]
    public void IsCallSuccessful_ShouldSucceed_WhenReturnValueIsZeroAndLastErrorIsClean()
    {
        // 返回值 0 是合法值（样式位恰好为 0 且 last-error 干净）。
        NativeWindowStyleInteropAdapter.IsCallSuccessful(0, 0).Should().BeTrue();
    }

    [Fact]
    public void IsCallSuccessful_ShouldFail_WhenReturnValueIsZeroAndLastErrorIndicatesFailure()
    {
        NativeWindowStyleInteropAdapter.IsCallSuccessful(0, 1400).Should().BeFalse();
        NativeWindowStyleInteropAdapter.IsCallSuccessful(0, 87).Should().BeFalse();
    }
}
