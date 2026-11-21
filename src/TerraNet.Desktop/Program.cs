using Avalonia;

namespace TerraNet.Desktop;

public static class Program
{
    public const string BackgroundFlag = "--background";
    private const string InstanceName = "TerraNet.Desktop.Instance";

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 1 && args[0] == ServerMode.Flag)
            return ServerMode.Run(args);

        var background = args.Contains(BackgroundFlag);
        using var instance = new Mutex(true, InstanceName, out var first);
        if (!first)
        {
            if (!background && EventWaitHandle.TryOpenExisting(TrayController.ShowSignal, out var show))
                using (show)
                    show.Set();
            return 0;
        }

        var platform = new DesktopPlatform();
        Directory.CreateDirectory(platform.DataDirectory);
        Core.Diagnostics.AppLog.Path = Path.Combine(platform.DataDirectory, "terranet.log");
        App.App.PlatformFactory = () => platform;
        App.App.DesktopReady = (desktop, window, main) => _ = new TrayController(desktop, window, main, background);

        return AppBuilder.Configure<App.App>()
            .UsePlatformDetect()
            .LogToTrace()
            .StartWithClassicDesktopLifetime(args);
    }
}
