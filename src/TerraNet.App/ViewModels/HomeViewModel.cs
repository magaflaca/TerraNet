using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TerraNet.App.Services;
using TerraNet.Core.Localization;
using TerraNet.Core.Session;

namespace TerraNet.App.ViewModels;

public sealed partial class HomeViewModel : ObservableObject
{
    private readonly AppHost _host;
    private readonly MainViewModel _main;
    private double? _preparing;
    private SessionSnapshot _snapshot = SessionSnapshot.Idle;

    public HomeViewModel(AppHost host, MainViewModel main)
    {
        _host = host;
        _main = main;
        _host.Worlds.Changed += () => Dispatcher.UIThread.Post(Reload);
        _host.PrepareProgress += p => Dispatcher.UIThread.Post(() =>
        {
            _preparing = p;
            Refresh();
        });
        Reload();
        _ = AttachAsync();
    }

    public ObservableCollection<WorldItemViewModel> Worlds { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private SessionView _session = SessionView.Idle;

    public bool IsIdle => !Session.IsActive;
    public bool HasWorlds => Worlds.Count > 0;
    public bool IsEmpty => Worlds.Count == 0;

    private async Task AttachAsync()
    {
        try
        {
            Bind(await _host.SessionAsync());
        }
        catch (Exception)
        {
        }
    }

    private void Bind(HostSession session)
    {
        session.Changed -= OnSessionChanged;
        session.Changed += OnSessionChanged;
        OnSessionChanged(session.Snapshot);
    }

    private void OnSessionChanged(SessionSnapshot snapshot) => Dispatcher.UIThread.Post(() =>
    {
        _snapshot = snapshot;
        if (snapshot.Server is not Core.Hosting.ServerState.Starting)
            _preparing = null;
        Refresh();
    });

    private void Refresh()
    {
        var previous = Session;
        Session = SessionView.From(_snapshot, _preparing);
        if (Session.IsRunning && !previous.IsRunning)
            Sfx.Play(Sound.Online);
        else if (Session.IsCrashed && !previous.IsCrashed)
            Sfx.Play(Sound.Error);
        else if (!Session.IsActive && previous.IsActive && !Session.IsCrashed)
            Sfx.Play(Sound.Stop);
        var live = Session.IsActive ? _snapshot.World?.Profile.Id : null;
        foreach (var world in Worlds)
            world.IsLive = world.Id == live;
    }

    private void Reload()
    {
        Worlds.Clear();
        foreach (var entry in _host.Worlds.List())
            Worlds.Add(new WorldItemViewModel(entry));
        OnPropertyChanged(nameof(HasWorlds));
        OnPropertyChanged(nameof(IsEmpty));
        Refresh();
    }

    [RelayCommand]
    private void NewWorld() => _main.Navigate(new WorldEditorViewModel(_host, _main));

    [RelayCommand]
    private async Task Host(WorldItemViewModel world)
    {
        _snapshot = _snapshot with { Server = new Core.Hosting.ServerState.Starting(), World = world.Entry };
        Refresh();
        try
        {
            var session = await _host.SessionAsync();
            Bind(session);
            session.Start(world.Id);
        }
        catch (Exception)
        {
            _snapshot = SessionSnapshot.Idle with { Server = new Core.Hosting.ServerState.Crashed(-1) };
            Refresh();
        }
    }

    [RelayCommand]
    private async Task Stop()
    {
        if (_host.Session is { } session)
            await session.StopAsync();
    }

    [RelayCommand]
    private async Task Delete(WorldItemViewModel world)
    {
        if (await _main.ConfirmAsync(Loc.Format("deleteWorld", world.Name), Loc.Get("delete")))
            _host.Worlds.Delete(world.Id);
    }

    [RelayCommand]
    private async Task Copy()
    {
        if (Session.Address is not { } address || _main.CopyText is not { } copy)
            return;
        await copy(address);
        Sfx.Play(Sound.Copy);
        _main.ShowToast(Loc.Get("copied"));
    }

    [RelayCommand]
    private async Task Share()
    {
        if (Session.Address is not { } address)
            return;
        var text = Loc.Format("shareText", Session.WorldName, address);
        if (await _host.Platform.ShareAsync(text) || _main.CopyText is not { } copy)
            return;
        await copy(text);
        _main.ShowToast(Loc.Get("copied"));
    }

    [RelayCommand]
    private void OpenConsole()
    {
        if (_host.Session is { } session)
            _main.Navigate(new ConsoleViewModel(session));
    }
}
