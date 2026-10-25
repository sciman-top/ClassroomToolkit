using System.Windows.Media;
using AwesomeAssertions;
using ClassroomToolkit.App.Paint;

namespace ClassroomToolkit.Tests.Paint;

[Collection("WPF UI")]
public sealed class CustomCursorsTests
{
    [Fact]
    public void GetBrushCursor_ShouldReuseCursor_ForTheSameColor()
    {
        WpfStaTestRunner.Run(() =>
        {
            var first = CustomCursors.GetBrushCursor(Colors.Red);
            var second = CustomCursors.GetBrushCursor(Colors.Red);
            var other = CustomCursors.GetBrushCursor(Colors.DodgerBlue);

            second.Should().BeSameAs(first);
            other.Should().NotBeSameAs(first);
        });
    }
}
