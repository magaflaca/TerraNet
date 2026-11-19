using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TerraNet.App.ViewModels;

public sealed partial class PromptViewModel(string title, string initial, string confirmText, int maxLength, Action close) : ObservableObject
{
    private readonly TaskCompletionSource<string?> _result = new();

    public string Title { get; } = title;
    public string ConfirmText { get; } = confirmText;
    public int MaxLength { get; } = maxLength;
    public Task<string?> Result => _result.Task;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private string _text = initial;

    private bool CanAccept() => !string.IsNullOrWhiteSpace(Text);

    [RelayCommand(CanExecute = nameof(CanAccept))]
    private void Accept() => Finish(Text.Trim());

    [RelayCommand]
    private void Cancel() => Finish(null);

    private void Finish(string? value)
    {
        close();
        _result.TrySetResult(value);
    }
}
