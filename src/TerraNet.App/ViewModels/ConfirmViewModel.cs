using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TerraNet.App.ViewModels;

public sealed partial class ConfirmViewModel(string message, string confirmText, Action close) : ObservableObject
{
    private readonly TaskCompletionSource<bool> _result = new();

    public string Message { get; } = message;
    public string ConfirmText { get; } = confirmText;
    public Task<bool> Result => _result.Task;

    [RelayCommand]
    private void Accept() => Finish(true);

    [RelayCommand]
    private void Cancel() => Finish(false);

    private void Finish(bool value)
    {
        close();
        _result.TrySetResult(value);
    }
}
