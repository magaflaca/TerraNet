using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Controls.Platform;
using Avalonia.Threading;
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
        if (top.InputPane is { } pane)
            pane.StateChanged += (_, args) =>
            {
                _keyboard = args.NewState == InputPaneState.Open ? args.EndRect.Height : 0;
                ApplyLayout(Bounds.Size);
                Dispatcher.UIThread.Post(RevealFocused, DispatcherPriority.Background);
            };
        AddHandler(GotFocusEvent, (_, args) =>
        {
            if (args.Source is TextBox)
                Dispatcher.UIThread.Post(RevealFocused, DispatcherPriority.Background);
        }, RoutingStrategies.Bubble);
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

    private const double WideBreakpoint = 820;
    private Thickness _insets;
    private double _keyboard;

    private void RevealFocused()
    {
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Control focused)
            focused.BringIntoView(new Rect(-8, -24, focused.Bounds.Width + 16, focused.Bounds.Height + 48));
    }

    private void ApplyInsets(Thickness padding)
    {
        _insets = padding;
        Resources["SafeAreaTop"] = new Thickness(0, padding.Top, 0, 0);
        Resources["SafeAreaBottom"] = new Thickness(0, 0, 0, padding.Bottom);
        ApplyLayout(Bounds.Size);
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e) => ApplyLayout(e.NewSize);

    private void ApplyLayout(Size size)
    {
        if (size.Width <= 0 || size.Height <= 0)
            return;
        if (size.Width >= WideBreakpoint)
        {
            const double gap = 28;
            Scene.Horizon = 0.76;
            Scene.FocusX = 0.22;
            Resources["SheetMargin"] = new Thickness(gap);
            Resources["SheetHorizontal"] = HorizontalAlignment.Right;
            Resources["SheetVertical"] = VerticalAlignment.Center;
            Resources["SheetWidth"] = Math.Min(600, size.Width * 0.52);
            Resources["SheetMaxHeight"] = size.Height - gap * 2;
            Resources["TallSheetMargin"] = new Thickness(gap);
            Resources["TallSheetVertical"] = VerticalAlignment.Center;
            Resources["TallSheetMaxHeight"] = size.Height - gap * 2;
            Resources["TitleMargin"] = new Thickness(32, 30, 0, 0);
            Resources["TitleSize"] = 52.0;
            return;
        }

        var bottom = Math.Max(_insets.Bottom, _keyboard);
        var horizon = Math.Round(Math.Max(210 + _insets.Top, size.Height * 0.32));
        Scene.Horizon = horizon / size.Height;
        Scene.FocusX = 0.5;
        var top = horizon + 22;
        Resources["SheetMargin"] = new Thickness(10, top, 10, bottom + 10);
        Resources["SheetHorizontal"] = HorizontalAlignment.Stretch;
        Resources["SheetVertical"] = VerticalAlignment.Top;
        Resources["SheetWidth"] = double.NaN;
        Resources["SheetMaxHeight"] = Math.Max(120, size.Height - top - bottom - 10);
        var tallTop = _insets.Top + 56;
        Resources["TallSheetMargin"] = new Thickness(10, tallTop, 10, bottom + 10);
        Resources["TallSheetVertical"] = VerticalAlignment.Stretch;
        Resources["TallSheetMaxHeight"] = Math.Max(120, size.Height - tallTop - bottom - 10);
        Resources["TitleMargin"] = new Thickness(20, _insets.Top + 18, 0, 0);
        Resources["TitleSize"] = 40.0;
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
