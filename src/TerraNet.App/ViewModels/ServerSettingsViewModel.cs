using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TerraNet.App.Services;
using TerraNet.Core.Hosting;
using TerraNet.Core.Localization;
using TerraNet.Core.Settings;

namespace TerraNet.App.ViewModels;

public sealed partial class ServerSettingsViewModel : ObservableObject
{
    private const int MinPlayers = 1;
    private const int MaxPlayersLimit = 32;
    private const int MinRadius = 1;
    private const int MaxRadius = 50;

    private readonly MainViewModel _main;
    private readonly SettingsService _settings;
    private readonly PluginLibrary _plugins;

    public ServerSettingsViewModel(MainViewModel main)
    {
        _main = main;
        _settings = main.Host.Settings;
        _plugins = main.Host.Plugins;
        var current = _settings.Current;
        _maxPlayers = current.MaxPlayers;
        _password = current.Password;
        _welcome = current.WelcomeMessage ?? Core.Hosting.Welcome.Default;
        _pvp = Choices.Find(Choices.PvpModes, current.Pvp);
        _spawnProtection = current.SpawnProtection;
        _spawnRadius = current.SpawnProtectionRadius;
        _antiCheat = current.AntiCheat;
        _playerCommands = current.PlayerCommands;
        _plugins.Changed += () => Dispatcher.UIThread.Post(ReloadPlugins);
        ReloadPlugins();
    }

    public IReadOnlyList<Choice> PvpModes => Choices.PvpModes;
    public ObservableCollection<PluginItemViewModel> Plugins { get; } = [];
    public bool HasPlugins => Plugins.Count > 0;
    public bool IsEmpty => Plugins.Count == 0;

    [ObservableProperty]
    private int _maxPlayers;

    [ObservableProperty]
    private string _password;

    [ObservableProperty]
    private string _welcome;

    [ObservableProperty]
    private Choice _pvp;

    [ObservableProperty]
    private bool _spawnProtection;

    [ObservableProperty]
    private int _spawnRadius;

    [ObservableProperty]
    private bool _antiCheat;

    [ObservableProperty]
    private bool _playerCommands;

    [ObservableProperty]
    private bool _includeWorlds;

    [ObservableProperty]
    private bool _exporting;

    partial void OnMaxPlayersChanged(int value) => _settings.Update(s => s with { MaxPlayers = value });
    partial void OnPasswordChanged(string value) => _settings.Update(s => s with { Password = value });
    partial void OnWelcomeChanged(string value) => _settings.Update(s => s with { WelcomeMessage = value });
    partial void OnPvpChanged(Choice value) => _settings.Update(s => s with { Pvp = (PvpMode)value.Value });
    partial void OnSpawnProtectionChanged(bool value) => _settings.Update(s => s with { SpawnProtection = value });
    partial void OnSpawnRadiusChanged(int value) => _settings.Update(s => s with { SpawnProtectionRadius = value });
    partial void OnAntiCheatChanged(bool value) => _settings.Update(s => s with { AntiCheat = value });
    partial void OnPlayerCommandsChanged(bool value) => _settings.Update(s => s with { PlayerCommands = value });

    [RelayCommand]
    private void MorePlayers() => MaxPlayers = Math.Min(MaxPlayersLimit, MaxPlayers + 1);

    [RelayCommand]
    private void FewerPlayers() => MaxPlayers = Math.Max(MinPlayers, MaxPlayers - 1);

    [RelayCommand]
    private void WiderSpawn() => SpawnRadius = Math.Min(MaxRadius, SpawnRadius + 1);

    [RelayCommand]
    private void NarrowerSpawn() => SpawnRadius = Math.Max(MinRadius, SpawnRadius - 1);

    [RelayCommand]
    private void OpenAllOptions() => _main.Navigate(new TShockOptionsViewModel(_main));

    [RelayCommand]
    private void ResetWelcome() => Welcome = Core.Hosting.Welcome.Default;

    [RelayCommand]
    private async Task AddPlugin()
    {
        if (_main.PickFiles is not { } pick)
            return;
        var files = await pick("Plugin", "*.dll");
        if (files.Count == 0)
            return;
        var added = 0;
        foreach (var file in files)
        {
            await using var stream = await file.OpenReadAsync();
            if (await _plugins.ImportAsync(stream, file.Name))
                added++;
        }
        Sfx.Play(added > 0 ? Sound.Create : Sound.Error);
        _main.ShowToast(Loc.Get(added > 0 ? "pluginAdded" : "pluginFailed"));
    }

    [RelayCommand]
    private async Task DeletePlugin(PluginItemViewModel plugin)
    {
        if (await _main.ConfirmAsync(Loc.Format("deletePlugin", plugin.Name), Loc.Get("delete")))
            _plugins.Delete(plugin.Name);
    }

    [RelayCommand]
    private async Task ExportServer()
    {
        if (Exporting || _main.SaveFile is not { } save || await save(ServerExport.SuggestedName()) is not { } file)
            return;
        Exporting = true;
        var exported = false;
        try
        {
            var host = _main.Host;
            var settings = _settings.Current;
            var includeWorlds = IncludeWorlds;
            await using var stream = await file.OpenWriteAsync();
            await Task.Run(() => ServerExport.WriteAsync(stream, host.Paths, settings, host.Worlds, _plugins, includeWorlds));
            exported = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Core.Diagnostics.AppLog.Error("export server", ex);
        }
        finally
        {
            Exporting = false;
        }
        Sfx.Play(exported ? Sound.Copy : Sound.Error);
        _main.ShowToast(Loc.Get(exported ? "serverExported" : "exportFailed"));
    }

    private void ReloadPlugins()
    {
        Plugins.Clear();
        foreach (var entry in _plugins.List())
            Plugins.Add(new PluginItemViewModel(entry, _plugins));
        OnPropertyChanged(nameof(HasPlugins));
        OnPropertyChanged(nameof(IsEmpty));
    }
}

public sealed partial class PluginItemViewModel(PluginEntry entry, PluginLibrary library) : ObservableObject
{
    public string Name { get; } = entry.Name;
    public string SizeLabel { get; } = $"{Math.Max(1, entry.SizeBytes / 1024)} KB";

    [ObservableProperty]
    private bool _enabled = entry.Enabled;

    partial void OnEnabledChanged(bool value)
    {
        library.SetEnabled(Name, value);
        Sfx.Play(Sound.Toggle);
    }
}
