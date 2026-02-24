using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class StylusPressureSignalAnalyzerTests
{
    [Fact]
    public void TryResolve_ShouldClassifyEndpointPseudoPressure_AsPointerOnly()
    {
        var analyzer = new StylusPressureSignalAnalyzer();
        var accepted = 0;

        for (int i = 0; i < 40; i++)
        {
            var sample = (i % 2 == 0) ? 0.0 : 1.0;
            if (analyzer.TryResolve(sample, 0.0001, 0.9999, 1.0, out _))
            {
                accepted++;
            }
        }

        accepted.Should().Be(0);
        analyzer.Profile.Should().Be(StylusPressureDeviceProfile.EndpointPseudo);
    }

    [Fact]
    public void TryResolve_ShouldAcceptContinuousPressure_AndApplyGammaCurve()
    {
        var analyzer = new StylusPressureSignalAnalyzer();
        var accepted = 0;
        var gamma = 1.35;
        double firstResolved = 0;

        for (int i = 0; i < 32; i++)
        {
            var sample = 0.12 + ((((System.Math.Sin(i * 0.33) + 1.0) * 0.5) * 0.76));
            if (analyzer.TryResolve(sample, 0.0001, 0.9999, gamma, out var resolved))
            {
                accepted++;
                if (firstResolved <= 0)
                {
                    firstResolved = resolved;
                }
            }
        }

        accepted.Should().BeGreaterThan(20);
        analyzer.Profile.Should().Be(StylusPressureDeviceProfile.Continuous);
        firstResolved.Should().BeGreaterThan(0);
        firstResolved.Should().BeLessThan(0.95);
    }

    [Fact]
    public void TryResolve_ShouldDowngradeLowRangePressure_ToPointer()
    {
        var analyzer = new StylusPressureSignalAnalyzer();
        var accepted = 0;
        var rejected = 0;

        for (int i = 0; i < 36; i++)
        {
            var sample = 0.5 + ((i % 3) - 1) * 0.006;
            if (analyzer.TryResolve(sample, 0.0001, 0.9999, 1.0, out _))
            {
                accepted++;
            }
            else
            {
                rejected++;
            }
        }

        rejected.Should().BeGreaterThan(accepted);
        analyzer.Profile.Should().Be(StylusPressureDeviceProfile.LowRange);
    }
}
