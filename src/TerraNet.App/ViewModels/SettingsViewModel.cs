using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TerraNet.Core.Localization;
using TerraNet.Core.Settings;

namespace TerraNet.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private const int MinPlayers = 1;
    private const int MaxPlayersLimit = 32;

    private readonly MainViewModel _main;
    private readonly SettingsService _settings;

    public SettingsViewModel(MainViewModel main)
    {
        _main = main;
        _settings = main.Host.Settings;
        var current = _settings.Current;
        _maxPlayers = current.MaxPlayers;
        _password = current.Password;
        _keepAwake = current.KeepAwake;
        _restartOnCrash = current.RestartOnCrash;
        _startOnBoot = current.StartOnBoot;
        _sounds = current.Sounds;
        _welcome = current.WelcomeMessage ?? Core.Hosting.Welcome.Default;
        _theme = Choices.Find(Choices.Themes, current.Theme);
    }

    public IReadOnlyList<Choice> Themes => Choices.Themes;
    public bool SupportsStartOnBoot => _main.Host.Platform.SupportsStartOnBoot;
    public string Version => _main.Host.Platform.AppVersion.ToString(3);

    [ObservableProperty]
    private int _maxPlayers;

    [ObservableProperty]
    private string _password;

    [ObservableProperty]
    private bool _keepAwake;

    [ObservableProperty]
    private bool _restartOnCrash;

    [ObservableProperty]
    private bool _startOnBoot;

    [ObservableProperty]
    private Choice _theme;

    [ObservableProperty]
    private bool _sounds;

    [ObservableProperty]
    private string _welcome;

    [ObservableProperty]
    private bool _isChecking;

    partial void OnMaxPlayersChanged(int value) => _settings.Update(s => s with { MaxPlayers = value });
    partial void OnPasswordChanged(string value) => _settings.Update(s => s with { Password = value });
    partial void OnKeepAwakeChanged(bool value) => _settings.Update(s => s with { KeepAwake = value });
    partial void OnRestartOnCrashChanged(bool value) => _settings.Update(s => s with { RestartOnCrash = value });
    partial void OnStartOnBootChanged(bool value) => _settings.Update(s => s with { StartOnBoot = value });
    partial void OnSoundsChanged(bool value) => _settings.Update(s => s with { Sounds = value });
    partial void OnWelcomeChanged(string value) => _settings.Update(s => s with { WelcomeMessage = value });

    [RelayCommand]
    private void ResetWelcome() => Welcome = Core.Hosting.Welcome.Default;

    partial void OnThemeChanged(Choice value)
    {
        var theme = (AppTheme)value.Value;
        _settings.Update(s => s with { Theme = theme });
        ApplyTheme(theme);
    }

    [RelayCommand]
    private void MorePlayers() => MaxPlayers = Math.Min(MaxPlayersLimit, MaxPlayers + 1);

    [RelayCommand]
    private void FewerPlayers() => MaxPlayers = Math.Max(MinPlayers, MaxPlayers - 1);

    [RelayCommand]
    private async Task CheckUpdates()
    {
        IsChecking = true;
        try
        {
            if (!await _main.CheckForUpdatesAsync())
                _main.ShowToast(Loc.Get("upToDate"));
        }
        finally
        {
            IsChecking = false;
        }
    }

    public static void ApplyTheme(AppTheme theme)
    {
        if (Application.Current is { } app)
            app.RequestedThemeVariant = theme switch
            {
                AppTheme.Light => ThemeVariant.Light,
                AppTheme.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default
            };
    }
}
