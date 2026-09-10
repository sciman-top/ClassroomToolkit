using System;
using System.Runtime.InteropServices;
using AwesomeAssertions;
using ClassroomToolkit.App.Windowing;
using ClassroomToolkit.Interop;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class DpiSuggestedBoundsInteropTests
{
    [Fact]
    public void TryCopy_ShouldReadAndValidateNativeRect()
    {
        var nativeRect = new NativeMethods.NativeRect
        {
            Left = -1920,
            Top = 0,
            Right = 0,
            Bottom = 1080
        };
        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<NativeMethods.NativeRect>());
        try
        {
            Marshal.StructureToPtr(nativeRect, pointer, fDeleteOld: false);

            var copied = DpiSuggestedBoundsInterop.TryCopy(pointer, out var suggestedBounds);

            copied.Should().BeTrue();
            suggestedBounds.Left.Should().Be(-1920);
            suggestedBounds.Top.Should().Be(0);
            suggestedBounds.Right.Should().Be(0);
            suggestedBounds.Bottom.Should().Be(1080);
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(10, 10, 5, 20)]
    public void TryCopy_ShouldRejectInvalidNativeRect(int left, int top, int right, int bottom)
    {
        var nativeRect = new NativeMethods.NativeRect
        {
            Left = left,
            Top = top,
            Right = right,
            Bottom = bottom
        };
        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<NativeMethods.NativeRect>());
        try
        {
            Marshal.StructureToPtr(nativeRect, pointer, fDeleteOld: false);

            DpiSuggestedBoundsInterop.TryCopy(pointer, out _).Should().BeFalse();
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    [Fact]
    public void TryCopy_ShouldRejectNullPointer()
    {
        DpiSuggestedBoundsInterop.TryCopy(IntPtr.Zero, out _).Should().BeFalse();
    }
}
