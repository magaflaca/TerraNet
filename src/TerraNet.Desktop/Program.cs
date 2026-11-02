using Avalonia;
using TerraNet.Desktop;
using Velopack;

VelopackApp.Build().Run();
TerraNet.App.App.PlatformFactory = () => new DesktopPlatform();

AppBuilder.Configure<TerraNet.App.App>()
    .UsePlatformDetect()
    .LogToTrace()
    .StartWithClassicDesktopLifetime(args);
