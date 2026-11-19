using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TerraNet.App.Services;
using TerraNet.Core.Session;

namespace TerraNet.App.ViewModels;

public sealed partial class BridgeViewModel : ObservableObject
{
    private const string DefaultHost = "127.0.0.1";
    private const int DefaultPort = 7777;
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(2);

    private readonly AppHost _host;
    private readonly MainViewModel _main;

    public BridgeViewModel(AppHost host, MainViewModel main)
    {
        _host = host;
        _main = main;
        _ = WatchAsync();
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private string _port = DefaultPort.ToString();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private string _address = DefaultHost;

    [ObservableProperty]
    private bool _showAdvanced;

    [ObservableProperty]
    private bool _detected;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private bool _isStarting;

    private int? ParsedPort => int.TryParse(Port, out var value) && value is > 0 and < 65536 ? value : null;

    private async Task WatchAsync()
    {
        await Task.Yield();
        while (_main.Page == this)
        {
            if (ParsedPort is { } port && !string.IsNullOrWhiteSpace(Address))
                Detected = await HostSession.ProbeAsync(Address.Trim(), port);
            await Task.Delay(ProbeInterval);
        }
    }

    [RelayCommand]
    private void ToggleAdvanced() => ShowAdvanced = !ShowAdvanced;

    private bool CanStart() => !IsStarting && ParsedPort is not null && !string.IsNullOrWhiteSpace(Address);

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task Start()
    {
        IsStarting = true;
        try
        {
            var session = await _host.SessionAsync();
            _main.Home.Attach(session);
            if (session.StartBridge(Address.Trim(), ParsedPort!.Value))
            {
                Sfx.Play(Sound.Online);
                _main.Back();
            }
            else
                Sfx.Play(Sound.Error);
        }
        catch (Exception ex)
        {
            Core.Diagnostics.AppLog.Error("bridge", ex);
            Sfx.Play(Sound.Error);
        }
        finally
        {
            IsStarting = false;
        }
    }
}
