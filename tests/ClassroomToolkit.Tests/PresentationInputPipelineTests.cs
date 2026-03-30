using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.Interop.Presentation;
using ClassroomToolkit.Services.Presentation;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PresentationInputPipelineTests
{
    [Theory]
    [InlineData("auto", InputStrategy.Auto)]
    [InlineData("raw", InputStrategy.Raw)]
    [InlineData("message", InputStrategy.Message)]
    [InlineData("invalid", InputStrategy.Auto)]
    public void ResolveInputStrategyMode_ShouldNormalize(string rawMode, InputStrategy expected)
    {
        var actual = PresentationInputPipeline.ResolveInputStrategyMode(rawMode);

        actual.Should().Be(expected);
    }

    [Fact]
    public void UpdateWpsMode_ShouldResetFallbackAndPersistStrategy()
    {
        var pipeline = CreatePipeline();
        pipeline.MarkWpsHookUnavailable();

        pipeline.UpdateWpsMode(WpsInputModeDefaults.Raw);

        pipeline.WpsForceMessageFallback.Should().BeFalse();
        pipeline.WpsStrategy.Should().Be(InputStrategy.Raw);
    }

    [Fact]
    public void ResolveWpsSendMode_Auto_ShouldUseTargetAvailability()
    {
        var pipeline = CreatePipeline();
        pipeline.UpdateWpsMode(WpsInputModeDefaults.Auto);

        var validResult = pipeline.ResolveWpsSendMode(targetIsValid: true);
        var emptyResult = pipeline.ResolveWpsSendMode(targetIsValid: false);

        validResult.Should().Be(InputStrategy.Raw);
        emptyResult.Should().Be(InputStrategy.Message);
    }

    [Fact]
    public void BuildWpsOptions_WheelAsKeyFromWheel_ShouldForceMessage()
    {
        var pipeline = CreatePipeline();
        pipeline.UpdateWpsMode(WpsInputModeDefaults.Raw);
        var baseOptions = new PresentationControlOptions
        {
            Strategy = InputStrategy.Raw,
            WheelAsKey = true,
            WpsDebounceMs = 120,
            LockStrategyWhenDegraded = true
        };

        var options = pipeline.BuildWpsOptions(baseOptions, source: "wheel");

        options.Strategy.Should().Be(InputStrategy.Message);
        options.AllowWps.Should().BeTrue();
        options.AllowOffice.Should().BeFalse();
    }

    [Fact]
    public void BuildOfficeOptions_ShouldUseOfficeStrategyAndOfficeChannel()
    {
        var pipeline = CreatePipeline();
        pipeline.UpdateOfficeMode(WpsInputModeDefaults.Raw);
        var baseOptions = new PresentationControlOptions
        {
            WheelAsKey = false,
            WpsDebounceMs = 160,
            LockStrategyWhenDegraded = false
        };

        var options = pipeline.BuildOfficeOptions(baseOptions);

        options.Strategy.Should().Be(InputStrategy.Raw);
        options.AllowOffice.Should().BeTrue();
        options.AllowWps.Should().BeFalse();
    }

    [Fact]
    public void BuildOptions_ShouldReturnSafeDefaults_WhenOptionsNull()
    {
        var pipeline = CreatePipeline();
        pipeline.UpdateOfficeMode(WpsInputModeDefaults.Raw);

#pragma warning disable CS8625
        var wps = pipeline.BuildWpsOptions(null);
        var office = pipeline.BuildOfficeOptions(null);
#pragma warning restore CS8625

        wps.AllowWps.Should().BeTrue();
        wps.AllowOffice.Should().BeFalse();
        wps.Strategy.Should().Be(InputStrategy.Message);

        office.AllowOffice.Should().BeTrue();
        office.AllowWps.Should().BeFalse();
        office.Strategy.Should().Be(InputStrategy.Raw);
    }

    private static PresentationInputPipeline CreatePipeline()
    {
        var classifier = new PresentationClassifier();
        var planner = new PresentationControlPlanner(classifier);
        var mapper = new PresentationCommandMapper();
        var sender = new NoopInputSender();
        var resolver = new Win32PresentationResolver();
        var validator = new PassThroughValidator();
        var service = new PresentationControlService(planner, mapper, sender, resolver, validator);
        return new PresentationInputPipeline(service);
    }

    private sealed class NoopInputSender : IInputSender
    {
        public bool SendKey(IntPtr hwnd, VirtualKey key, KeyModifiers modifiers, InputStrategy strategy, bool keyDownOnly)
        {
            return true;
        }

        public bool SendWheel(IntPtr hwnd, int delta, InputStrategy strategy)
        {
            return true;
        }
    }

    private sealed class PassThroughValidator : IPresentationWindowValidator
    {
        public bool IsWindowValid(IntPtr hwnd)
        {
            return hwnd != IntPtr.Zero;
        }
    }
}
