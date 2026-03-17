using System.Runtime.InteropServices;
using ClassroomToolkit.Interop.Utilities;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ComObjectManagerIntegrationTests
{
    [Fact]
    public void Release_ShouldBeIdempotent_ForTrackedComObject()
    {
        var comObject = TryCreateComObject();
        if (comObject is null)
        {
            return;
        }

        try
        {
            using var manager = new ComObjectManager();
            var tracked = manager.Track(comObject);

            Marshal.IsComObject(tracked).Should().BeTrue();
            manager.Release(tracked);
            manager.Release(tracked);
        }
        finally
        {
            TryFinalReleaseComObject(comObject);
        }
    }

    [Fact]
    public void Track_OnDisposedManager_ShouldThrow_ForComObject()
    {
        var comObject = TryCreateComObject();
        if (comObject is null)
        {
            return;
        }

        try
        {
            using var manager = new ComObjectManager();
            manager.Dispose();

            var act = () => manager.Track(comObject);

            act.Should().Throw<ObjectDisposedException>();
        }
        finally
        {
            TryFinalReleaseComObject(comObject);
        }
    }

    private static object? TryCreateComObject()
    {
        var progIds = new[]
        {
            "Scripting.Dictionary",
            "WScript.Shell",
            "Shell.Application"
        };

        foreach (var progId in progIds)
        {
            try
            {
                var type = Type.GetTypeFromProgID(progId, throwOnError: false);
                if (type is null)
                {
                    continue;
                }

                var instance = Activator.CreateInstance(type);
                if (instance is not null && Marshal.IsComObject(instance))
                {
                    return instance;
                }
            }
            catch
            {
                // Try next COM provider.
            }
        }

        return null;
    }

    private static void TryFinalReleaseComObject(object comObject)
    {
        if (!Marshal.IsComObject(comObject))
        {
            return;
        }

        try
        {
            Marshal.FinalReleaseComObject(comObject);
        }
        catch
        {
            // Ignore cleanup failures in integration test teardown.
        }
    }
}
