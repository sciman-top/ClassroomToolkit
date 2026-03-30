namespace ClassroomToolkit.App.Paint.Brushes;

internal static class InkRendererFactoryResolver
{
    private const string GpuExperimentalFlag = "CTOOLKIT_ENABLE_EXPERIMENTAL_GPU_INK";

    private static bool IsGpuBackendAvailable()
    {
        var flag = Environment.GetEnvironmentVariable(GpuExperimentalFlag);
        if (!string.Equals(flag, "1", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Reserved for future GPU pipeline probing (Skia/D3D runtime checks).
        // Flag gate already keeps this off by default.
        return true;
    }

    internal static IInkRendererFactory Resolve(bool preferGpu, out string requestedBackend)
        => Resolve(preferGpu, IsGpuBackendAvailable, out requestedBackend);

    internal static IInkRendererFactory Resolve(
        bool preferGpu,
        Func<bool> gpuAvailabilityProbe,
        out string requestedBackend)
    {
        ArgumentNullException.ThrowIfNull(gpuAvailabilityProbe);

        requestedBackend = preferGpu ? "gpu" : "cpu";
        var selected = InkRendererBackendSelectionPolicy.Resolve(
            preferGpu,
            gpuAvailable: gpuAvailabilityProbe());

        return selected switch
        {
            InkRendererBackendKind.Gpu => new GpuInkRendererFactory(),
            _ => new CpuInkRendererFactory()
        };
    }
}
