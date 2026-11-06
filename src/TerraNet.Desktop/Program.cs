using Avalonia;
using TerraNet.Desktop;
using Velopack;

VelopackApp.Build().Run();
var platform = new DesktopPlatform();
Directory.CreateDirectory(platform.DataDirectory);
TerraNet.Core.Diagnostics.AppLog.Path = Path.Combine(platform.DataDirectory, "terranet.log");
TerraNet.App.App.PlatformFactory = () => platform;

AppBuilder.Configure<TerraNet.App.App>()
    .UsePlatformDetect()
    .LogToTrace()
    .StartWithClassicDesktopLifetime(args);
