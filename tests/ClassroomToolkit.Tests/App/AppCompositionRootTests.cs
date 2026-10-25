using ClassroomToolkit.App.Startup;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomToolkit.Tests.App;

[Collection("WPF UI")]
public sealed class AppCompositionRootTests
{
    [Fact]
    public void RegisteredImplementations_ShouldHaveValidConstructorDependencies_WithoutActivatingServices()
    {
        WpfStaTestRunner.Run(() =>
        {
            var services = AppCompositionRoot.CreateServices(
                System.Windows.Application.Current,
                Path.Combine(Path.GetTempPath(), "ClassroomToolkit-composition-test"));

            // Validate constructors across the shell and feature factories. Do not resolve
            // services: their lazy factories load user settings and wire runtime adapters.
            using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
        });
    }
}
