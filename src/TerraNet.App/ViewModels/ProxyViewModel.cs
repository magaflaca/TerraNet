using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TerraNet.App.Services;
using TerraNet.Core.Localization;
using TerraNet.Core.Proxy;
using TerraNet.Core.Worlds;

namespace TerraNet.App.ViewModels;

public sealed partial class ProxyViewModel : ObservableObject
{
    private readonly AppHost _host;
    private readonly MainViewModel _main;

    public ProxyViewModel(AppHost host, MainViewModel main)
    {
        _host = host;
        _main = main;
        var settings = host.Settings.Current;
        _address = settings.ProxyHost;
        _port = settings.ProxyPort.ToString();
        _snapshot = host.Proxy.Snapshot;
        host.Proxy.Changed += OnProxyChanged;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private string _address;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private string _port;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSetup), nameof(IsProbing), nameof(IsRunning), nameof(IsFailed), nameof(ErrorText),
        nameof(WorldName), nameof(SizeLabel), nameof(DifficultyLabel), nameof(DifficultySprite), nameof(EvilSprite),
        nameof(VersionLabel), nameof(IsLegacy), nameof(IsProtected), nameof(ClientsText), nameof(HasServerPlayers),
        nameof(ServerPlayersText), nameof(LocalEndpoint), nameof(HasLocalEndpoint), nameof(RemoteEndpoint),
        nameof(IsTranslating), nameof(IsUnreachable), nameof(IsVisible), nameof(IsActive))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private ProxySnapshot _snapshot;

    public bool IsActive => Snapshot.IsActive;
    public bool IsSetup => Snapshot.Status is ProxyStatus.Idle or ProxyStatus.Failed;
    public bool IsProbing => Snapshot.Status == ProxyStatus.Probing;
    public bool IsRunning => Snapshot.Status == ProxyStatus.Running;
    public bool IsFailed => Snapshot.Status == ProxyStatus.Failed;
    public bool IsUnreachable => IsRunning && Snapshot.Unreachable;
    public bool IsVisible => IsRunning && !Snapshot.Unreachable;
    public bool IsTranslating => IsRunning && Snapshot.Translating;

    public string ErrorText => Loc.Get(Snapshot.Error switch
    {
        ProbeError.Unreachable => "proxyUnreachable",
        ProbeError.Rejected => "proxyRejected",
        ProbeError.NoAnswer => "proxyNoAnswer",
        _ => "proxyFailed"
    });

    private RemoteWorld? World => Snapshot.World;
    public string WorldName => World?.Name ?? "";
    public string SizeLabel => Loc.Get(Size.ToString().ToLowerInvariant());
    public string DifficultyLabel => Loc.Get(Difficulty.ToString().ToLowerInvariant());
    public string DifficultySprite => Choices.DifficultySprite(Difficulty);
    public string EvilSprite => Choices.EvilSprite(World?.Crimson == true ? WorldEvil.Crimson : WorldEvil.Corruption);
    public string VersionLabel => World?.Modern == true ? "1.4.6" : "1.4.5";
    public bool IsLegacy => World is { Modern: false };
    public bool IsProtected => World?.Protected == true;
    public string ClientsText => Snapshot.Clients.ToString();
    public bool HasServerPlayers => World?.PlayersKnown == true;
    public string ServerPlayersText => World is { } world ? $"{world.Players}/{world.MaxPlayers}" : "";
    public bool HasLocalEndpoint => Snapshot.LocalAddress is not null;
    public string LocalEndpoint => $"{Snapshot.LocalAddress}:{Snapshot.LocalPort}";
    public string RemoteEndpoint => $"{Snapshot.Host}:{Snapshot.Port}";

    private WorldSize Size => World?.Width switch
    {
        null or <= 4200 => WorldSize.Small,
        <= 6400 => WorldSize.Medium,
        _ => WorldSize.Large
    };

    private WorldDifficulty Difficulty => World?.GameMode is { } mode && Enum.IsDefined(typeof(WorldDifficulty), mode)
        ? (WorldDifficulty)mode
        : WorldDifficulty.Classic;

    private void OnProxyChanged(ProxySnapshot snapshot) => Dispatcher.UIThread.Post(() =>
    {
        var previous = Snapshot.Status;
        Snapshot = snapshot;
        if (previous == ProxyStatus.Probing && snapshot.Status == ProxyStatus.Running)
            Sfx.Play(Sound.Online);
        else if (previous == ProxyStatus.Probing && snapshot.Status == ProxyStatus.Failed)
            Sfx.Play(Sound.Error);
    });

    public void Detach() => _host.Proxy.Changed -= OnProxyChanged;

    private (string Host, int Port)? Target()
    {
        var text = Address.Trim();
        if (text.Length == 0)
            return null;
        var port = int.TryParse(Port.Trim(), out var value) ? value : 0;
        var colon = text.LastIndexOf(':');
        if (colon > 0 && text.IndexOf(':') == colon && int.TryParse(text[(colon + 1)..], out var inline))
        {
            port = inline;
            text = text[..colon];
        }
        if (text.StartsWith('[') && text.EndsWith(']'))
            text = text[1..^1];
        return port is > 0 and < 65536 && text.Length > 0 ? (text, port) : null;
    }

    private bool CanStart() => IsSetup && Target() is not null;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
        if (Target() is not { } target)
            return;
        Address = target.Host;
        Port = target.Port.ToString();
        _host.Settings.Update(s => s with { ProxyHost = target.Host, ProxyPort = target.Port });
        Sfx.Play(Sound.Click);
        _host.Proxy.Start(target.Host, target.Port);
    }

    [RelayCommand]
    private void Stop()
    {
        _host.Proxy.Stop();
        Sfx.Play(Sound.Stop);
    }

    [RelayCommand]
    private async Task CopyLocal()
    {
        if (_main.CopyText is { } copy && HasLocalEndpoint)
        {
            await copy(LocalEndpoint);
            Sfx.Play(Sound.Copy);
            _main.ShowToast(Loc.Get("copied"));
        }
    }
}
