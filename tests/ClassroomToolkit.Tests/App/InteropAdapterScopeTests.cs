using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class InteropAdapterScopeTests
{
    [Fact]
    public void Create_ShouldInvokeRestoreOnlyOnce()
    {
        var callCount = 0;
        var scope = InteropAdapterScope.Create(() => callCount++);

        scope.Dispose();
        scope.Dispose();

        callCount.Should().Be(1);
    }
}
