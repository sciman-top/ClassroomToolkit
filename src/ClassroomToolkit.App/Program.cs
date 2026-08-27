using System;
using Velopack;

namespace ClassroomToolkit.App;

internal static class Program
{
    [STAThread]
    public static void Main()
    {
        VelopackApp.Build().Run();

        var application = new App();
        application.InitializeComponent();
        application.Run();
    }
}
