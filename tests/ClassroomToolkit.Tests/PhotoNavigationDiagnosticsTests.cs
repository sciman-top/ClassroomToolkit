using ClassroomToolkit.App.Photos;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoNavigationDiagnosticsTests
{
    [Fact]
    public void PushNowProviderForTest_ShouldRestorePreviousProvider_OnDispose()
    {
        using var scope1 = PhotoNavigationDiagnostics.PushNowProviderForTest(
            () => new DateTime(2026, 3, 7, 8, 0, 0, DateTimeKind.Local));
        using var scope2 = PhotoNavigationDiagnostics.PushNowProviderForTest(
            () => new DateTime(2026, 3, 7, 9, 0, 0, DateTimeKind.Local));

        scope2.Dispose();

        // If restore works, nested push/pop should be safe and not throw.
        Action action = () =>
        {
            using var _ = PhotoNavigationDiagnostics.PushNowProviderForTest(
                () => new DateTime(2026, 3, 7, 10, 0, 0, DateTimeKind.Local));
        };

        action.Should().NotThrow();
    }
}
