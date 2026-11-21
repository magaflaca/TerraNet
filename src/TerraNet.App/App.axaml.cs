using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TerraNet.App.Services;
using TerraNet.App.ViewModels;
using TerraNet.App.Views;

namespace TerraNet.App;

public partial class App : Application
{
    public static Func<IPlatform>? PlatformFactory { get; set; }

    public static Action<IClassicDesktopStyleApplicationLifetime, MainWindow, MainViewModel>? DesktopReady { get; set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var host = AppHost.Initialize(PlatformFactory?.Invoke() ?? throw new InvalidOperationException("platform not configured"));
        Core.Localization.Loc.Use(host.Settings.Current.Language);
        SettingsViewModel.ApplyTheme(host.Settings.Current.Theme);
        SoundHooks.Install();

        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                var main = new MainViewModel(host);
                var window = new MainWindow { DataContext = main };
                desktop.MainWindow = window;
                desktop.ShutdownRequested += (_, e) => StopBeforeExit(host, desktop, e);
                DesktopReady?.Invoke(desktop, window, main);
                break;
            case IActivityApplicationLifetime activity:
                activity.MainViewFactory = () => new MainView { DataContext = new MainViewModel(host) };
                break;
            case ISingleViewApplicationLifetime single:
                single.MainView = new MainView { DataContext = new MainViewModel(host) };
                break;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async void StopBeforeExit(AppHost host, IClassicDesktopStyleApplicationLifetime desktop, ShutdownRequestedEventArgs e)
    {
        if (host.Session is not { Snapshot.IsActive: true } session)
            return;
        e.Cancel = true;
        await session.StopAsync();
        desktop.Shutdown();
    }
}
