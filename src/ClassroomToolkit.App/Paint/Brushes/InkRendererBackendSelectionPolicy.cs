namespace ClassroomToolkit.App.Paint.Brushes;

internal static class InkRendererBackendSelectionPolicy
{
    internal static InkRendererBackendKind Resolve(bool preferGpu, bool gpuAvailable)
    {
        return preferGpu && gpuAvailable
            ? InkRendererBackendKind.Gpu
            : InkRendererBackendKind.Cpu;
    }
}
