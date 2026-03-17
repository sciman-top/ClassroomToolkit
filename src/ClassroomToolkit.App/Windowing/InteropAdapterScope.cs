using System;

namespace ClassroomToolkit.App.Windowing;

internal static class InteropAdapterScope
{
    internal static IDisposable Create(Action restore)
    {
        ArgumentNullException.ThrowIfNull(restore);
        return new Scope(restore);
    }

    private sealed class Scope : IDisposable
    {
        private readonly Action _restore;
        private bool _disposed;

        internal Scope(Action restore)
        {
            ArgumentNullException.ThrowIfNull(restore);
            _restore = restore;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _restore();
        }
    }
}
