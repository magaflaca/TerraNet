using Avalonia;
using TerraNet.Desktop;

if (args.Length > 1 && args[0] == ServerMode.Flag)
    return ServerMode.Run(args);

using var instance = new Mutex(true, "TerraNet.Desktop.Instance", out var first);
if (!first)
    return 0;

var platform = new DesktopPlatform();
Directory.CreateDirectory(platform.DataDirectory);
TerraNet.Core.Diagnostics.AppLog.Path = Path.Combine(platform.DataDirectory, "terranet.log");
TerraNet.App.App.PlatformFactory = () => platform;

return AppBuilder.Configure<TerraNet.App.App>()
    .UsePlatformDetect()
    .LogToTrace()
    .StartWithClassicDesktopLifetime(args);
