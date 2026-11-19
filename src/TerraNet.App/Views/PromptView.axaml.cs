using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TerraNet.App.Views;

public partial class PromptView : UserControl
{
    public PromptView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Dispatcher.UIThread.Post(() =>
        {
            Input.Focus();
            Input.SelectAll();
        });
    }
}
