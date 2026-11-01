using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using TerraNet.Core.Localization;
using TerraNet.Core.Updates;

namespace TerraNet.App.ViewModels;

public sealed partial class UpdateViewModel(ReleaseInfo release, MainViewModel main) : ObservableObject
{
    public string Version { get; } = release.Version;
    public IReadOnlyList<string> Notes { get; } = release.LocalizedNotes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isDownloading;

    [ObservableProperty]
    private double _progress;

    public bool IsIdle => !IsDownloading;

    [RelayCommand]
    private async Task Update()
    {
        IsDownloading = true;
        try
        {
            var progress = new Progress<double>(p => Dispatcher.UIThread.Post(() => Progress = p));
            await main.Host.Platform.InstallUpdateAsync(release, progress);
        }
        catch (Exception)
        {
            main.ShowToast(Loc.Get("updateFailed"));
        }
        finally
        {
            IsDownloading = false;
        }
    }

    [RelayCommand]
    private void Later() => main.Dialog = null;
}
