using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using TerraNet.App.ViewModels;

namespace TerraNet.App.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
            return;

        if (ViewModel is { } vm)
            vm.CopyText = async text =>
            {
                if (top.Clipboard is { } clipboard)
                    await clipboard.SetTextAsync(text);
            };

        top.BackRequested += OnBackRequested;
        if (top.InsetsManager is { } insets)
        {
            insets.DisplayEdgeToEdgePreference = true;
            ApplyInsets(insets.SafeAreaPadding);
            insets.SafeAreaChanged += (_, args) => ApplyInsets(args.SafeAreaPadding);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is { } top)
            top.BackRequested -= OnBackRequested;
        base.OnDetachedFromVisualTree(e);
    }

    private void ApplyInsets(Thickness padding)
    {
        Resources["SafeAreaTop"] = new Thickness(0, padding.Top, 0, 0);
        Resources["SafeAreaBottom"] = new Thickness(0, 0, 0, padding.Bottom);
    }

    private void OnBackRequested(object? sender, RoutedEventArgs e)
    {
        if (ViewModel?.Back() == true)
            e.Handled = true;
    }

    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel?.Dialog is UpdateViewModel { IsDownloading: true })
            return;
        ViewModel?.Back();
    }
}
