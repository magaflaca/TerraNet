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

    public void Attach(HostSession session) => Bind(session);

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
            Core.Diagnostics.AppLog.Write("host requested " + world.Id);
            var session = await _host.SessionAsync();
            Bind(session);
            Core.Diagnostics.AppLog.Write("session started " + session.Start(world.Id));
        }
        catch (Exception ex)
        {
            Core.Diagnostics.AppLog.Error("host", ex);
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
    private async Task RenameWorld(WorldItemViewModel world)
    {
        var name = await _main.PromptAsync(Loc.Get("rename"), world.Name, Loc.Get("save"), WorldNames.MaxLength);
        if (name is null || name == world.Name)
            return;
        var renamed = _host.Worlds.Rename(world.Id, name);
        Sfx.Play(renamed ? Sound.Create : Sound.Error);
        if (!renamed)
            _main.ShowToast(Loc.Get("renameFailed"));
    }

    [RelayCommand]
    private async Task ExportWorld(WorldItemViewModel world)
    {
        if (_main.SaveFile is not { } save || await save(world.Name + ".wld") is not { } file)
            return;
        var exported = false;
        try
        {
            await using var stream = await file.OpenWriteAsync();
            exported = await _host.Worlds.ExportAsync(world.Id, stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Core.Diagnostics.AppLog.Error("export", ex);
        }
        Sfx.Play(exported ? Sound.Copy : Sound.Error);
        _main.ShowToast(Loc.Get(exported ? "exported" : "exportFailed"));
    }

    [RelayCommand]
    private Task CopyHost() => CopyAsync(Session.Host);

    [RelayCommand]
    private Task CopyPort() => CopyAsync(Session.Port);

    private async Task CopyAsync(string text)
    {
        if (string.IsNullOrEmpty(text) || _main.CopyText is not { } copy)
            return;
        await copy(text);
        Sfx.Play(Sound.Copy);
        _main.ShowToast(Loc.Get("copied"));
    }

    [RelayCommand]
    private async Task Share()
    {
        if (!Session.HasAddress)
            return;
        var text = Loc.Format("shareText", Session.WorldName, Session.Host, Session.Port);
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
