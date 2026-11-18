using Avalonia;

namespace TerraNet.Desktop;

public static class Program
{
    private const string InstanceName = "TerraNet.Desktop.Instance";

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 1 && args[0] == ServerMode.Flag)
            return ServerMode.Run(args);

        using var instance = new Mutex(true, InstanceName, out var first);
        if (!first)
            return 0;

        var platform = new DesktopPlatform();
        Directory.CreateDirectory(platform.DataDirectory);
        Core.Diagnostics.AppLog.Path = Path.Combine(platform.DataDirectory, "terranet.log");
        App.App.PlatformFactory = () => platform;

        return AppBuilder.Configure<App.App>()
            .UsePlatformDetect()
            .LogToTrace()
            .StartWithClassicDesktopLifetime(args);
    }
}
