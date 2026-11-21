using System.ComponentModel;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using TerraNet.App.Controls;
using TerraNet.App.ViewModels;
using TerraNet.App.Views;
using TerraNet.Core.Localization;
using TerraNet.Core.Settings;

namespace TerraNet.Desktop;

public sealed class TrayController
{
    public const string ShowSignal = "TerraNet.Desktop.Show";

    private readonly IClassicDesktopStyleApplicationLifetime _desktop;
    private readonly MainWindow _window;
    private readonly MainViewModel _main;
    private readonly SettingsService _settings;
    private readonly WindowIcon _idle = LoadIcon("TerraNet.TrayIdle.ico");
    private readonly WindowIcon _online = LoadIcon("TerraNet.TrayOnline.ico");
    private readonly TrayIcon _icon;
    private readonly NativeMenuItem _stop;
    private bool _quitting;
    private bool _hidden;

    public TrayController(IClassicDesktopStyleApplicationLifetime desktop, MainWindow window, MainViewModel main, bool background)
    {
        _desktop = desktop;
        _window = window;
        _main = main;
        _settings = main.Host.Settings;

        var open = new NativeMenuItem(Loc.Get("trayOpen"));
        open.Click += (_, _) => Show();
        _stop = new NativeMenuItem(Loc.Get("trayStop"));
        _stop.Click += (_, _) => _main.Home.StopCommand.Execute(null);
        var quit = new NativeMenuItem(Loc.Get("trayQuit"));
        quit.Click += (_, _) => Quit();

        _icon = new TrayIcon
        {
            Icon = _idle,
            ToolTipText = "TerraNet",
            Menu = new NativeMenu { open, new NativeMenuItemSeparator(), _stop, new NativeMenuItemSeparator(), quit }
        };
        _icon.Clicked += (_, _) => Show();
        TrayIcon.SetIcons(Application.Current!, [_icon]);

        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        window.Closing += OnClosing;
        _settings.Changed += _ => Dispatcher.UIThread.Post(Refresh);
        _main.Home.PropertyChanged += OnHomeChanged;
        if (_settings.Current.StartOnBoot)
            main.Host.Platform.ApplyStartOnBoot(true);

        if (background)
            StartInBackground();
        Refresh();
        ListenForShowRequests();
    }

    private void StartInBackground()
    {
        if (_settings.Current.KeepInTray)
        {
            _desktop.MainWindow = null;
            _hidden = true;
        }
        else
            _window.WindowState = WindowState.Minimized;

        if (_settings.Current.LastWorldId is { } id && _main.Home.Worlds.FirstOrDefault(w => w.Id == id) is { } world)
            _main.Home.HostCommand.Execute(world);
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_quitting)
            return;
        if (_settings.Current.KeepInTray)
        {
            e.Cancel = true;
            _window.Hide();
            _hidden = true;
            Refresh();
            var session = _main.Home.Session;
            WindowsToast.Show(Loc.Get("toastTitle"), session.IsActive ? Loc.Format("toastOnline", session.WorldName) : Loc.Get("toastIdle"));
            return;
        }
        Quit();
    }

    private void Show()
    {
        _desktop.MainWindow ??= _window;
        _window.Show();
        if (_window.WindowState == WindowState.Minimized)
            _window.WindowState = WindowState.Normal;
        _window.Activate();
        _hidden = false;
        Refresh();
    }

    private void Quit()
    {
        _quitting = true;
        _icon.IsVisible = false;
        _desktop.Shutdown();
    }

    private void OnHomeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HomeViewModel.Session))
            Refresh();
    }

    private void Refresh()
    {
        var session = _main.Home.Session;
        var online = session.Mood == SceneMood.Online;
        _icon.Icon = online ? _online : _idle;
        _icon.ToolTipText = session.IsActive
            ? $"TerraNet · {Loc.Format("trayOnline", session.WorldName)}{(session.HasAddress ? $" · {session.Address}" : "")}"
            : $"TerraNet · {Loc.Get("trayIdle")}";
        _stop.IsEnabled = session.IsActive;
        _icon.IsVisible = !_quitting && (_settings.Current.KeepInTray || _hidden);
    }

    private void ListenForShowRequests()
    {
        var signal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignal);
        var thread = new Thread(() =>
        {
            while (signal.WaitOne())
                Dispatcher.UIThread.Post(Show);
        }) { IsBackground = true, Name = "TerraNet show signal" };
        thread.Start();
    }

    private static WindowIcon LoadIcon(string resource)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
                           ?? throw new InvalidOperationException(resource + " missing");
        return new WindowIcon(stream);
    }
}
