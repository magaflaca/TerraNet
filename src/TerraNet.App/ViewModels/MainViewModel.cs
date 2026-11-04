using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TerraNet.App.Services;

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
