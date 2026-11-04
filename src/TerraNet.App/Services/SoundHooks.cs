using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using TerraNet.App.Controls;

namespace TerraNet.App.Services;

public static class SoundHooks
{
    private static bool _installed;

    public static void Install()
    {
        if (_installed)
            return;
        _installed = true;
        Button.ClickEvent.AddClassHandler<Button>(OnClick, RoutingStrategies.Bubble, handledEventsToo: true);
        SelectingItemsControl.SelectionChangedEvent.AddClassHandler<ListBox>(OnSelection, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private static void OnClick(Button button, RoutedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, button) || button.Classes.Contains("quiet"))
            return;
        Sfx.Play(button is PixelSwitch ? Sound.Toggle : Sound.Click);
    }

    private static void OnSelection(ListBox list, SelectionChangedEventArgs e)
    {
        if (ReferenceEquals(e.Source, list) && e.RemovedItems.Count > 0 && list.IsKeyboardFocusWithin | list.IsPointerOver)
            Sfx.Play(Sound.Select);
    }
}
