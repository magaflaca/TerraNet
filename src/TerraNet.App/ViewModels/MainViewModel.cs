using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TerraNet.App.Services;
using TerraNet.Core.Localization;

namespace TerraNet.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(1.8);
    private readonly Stack<ObservableObject> _history = new();

    public MainViewModel(AppHost host)
    {
        Host = host;
        Home = new HomeViewModel(host, this);
        _page = Home;
        _ = CheckForUpdatesAsync();
    }

    public AppHost Host { get; }
    public HomeViewModel Home { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    private ObservableObject _page;

    [ObservableProperty]
    private ObservableObject? _dialog;

    partial void OnDialogChanged(ObservableObject? value)
    {
        if (value is not null)
            Sfx.Play(Sound.Pop);
    }

    [ObservableProperty]
    private string? _toast;

    public bool CanGoBack => _history.Count > 0;

    public Func<string, Task>? CopyText { get; set; }

    public Func<string, string, Task<IReadOnlyList<IStorageFile>>>? PickFiles { get; set; }

    public Func<string, Task<IStorageFile?>>? SaveFile { get; set; }

    [ObservableProperty]
    private bool _isDropping;

    [RelayCommand]
    private async Task Import()
    {
        if (PickFiles is { } pick)
            await ImportAsync(await pick("Terraria", "*.wld"));
    }

    public async Task ImportAsync(IEnumerable<IStorageItem> items)
    {
        var files = items.OfType<IStorageFile>().ToList();
        if (files.Count == 0)
            return;
        var imported = 0;
        foreach (var file in files)
        {
            try
            {
                await using var stream = await file.OpenReadAsync();
                if (await Host.Worlds.ImportAsync(stream, file.Name) is not null)
                    imported++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Core.Diagnostics.AppLog.Error("import", ex);
            }
        }
        Sfx.Play(imported > 0 ? Sound.Create : Sound.Error);
        ShowToast(Loc.Get(imported > 0 ? "imported" : "importFailed"));
    }

    public void Navigate(ObservableObject page)
    {
        _history.Push(Page);
        Page = page;
    }

    [RelayCommand]
    private void GoBack() => Back();

    public bool Back()
    {
        if (Dialog is ConfirmViewModel confirm)
        {
            confirm.CancelCommand.Execute(null);
            return true;
        }
        if (Dialog is PromptViewModel prompt)
        {
            prompt.CancelCommand.Execute(null);
            return true;
        }
        if (Dialog is not null)
        {
            Dialog = null;
            return true;
        }
        if (_history.Count == 0)
            return false;
        Page = _history.Pop();
        return true;
    }

    [RelayCommand]
    private void OpenSettings() => Navigate(new SettingsViewModel(this));

    public async void ShowToast(string text)
    {
        Toast = text;
        await Task.Delay(ToastDuration);
        if (Toast == text)
            Toast = null;
    }

    public void SetTheme(Core.Settings.AppTheme theme)
    {
        Host.Settings.Update(s => s with { Theme = theme });
        SettingsViewModel.ApplyTheme(theme);
        Sfx.Play(Sound.Toggle);
    }

    public Task<string?> PromptAsync(string title, string initial, string confirm, int maxLength)
    {
        var dialog = new PromptViewModel(title, initial, confirm, maxLength, () => Dialog = null);
        Dialog = dialog;
        return dialog.Result;
    }

    [RelayCommand]
    private void OpenServer() => Navigate(new ServerSettingsViewModel(this));

    [RelayCommand]
    private void OpenBridge() => Navigate(new BridgeViewModel(Host, this));

    public Task<bool> ConfirmAsync(string message, string confirm)
    {
        var dialog = new ConfirmViewModel(message, confirm, () => Dialog = null);
        Dialog = dialog;
        return dialog.Result;
    }

    public async Task<bool> CheckForUpdatesAsync()
    {
        var release = await Host.Updates.CheckAsync();
        if (release is null)
            return false;
        Dialog = new UpdateViewModel(release, this);
        return true;
    }
}
