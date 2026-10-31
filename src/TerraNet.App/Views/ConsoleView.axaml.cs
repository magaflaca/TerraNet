using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using TerraNet.App.ViewModels;

namespace TerraNet.App.Views;

public partial class ConsoleView : UserControl
{
    private ConsoleViewModel? _attached;

    public ConsoleView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_attached is not null)
            _attached.LineAdded -= ScrollToEnd;
        _attached = DataContext as ConsoleViewModel;
        if (_attached is not null)
            _attached.LineAdded += ScrollToEnd;
        ScrollToEnd();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_attached is not null)
        {
            _attached.LineAdded -= ScrollToEnd;
            _attached.Detach();
        }
        base.OnDetachedFromVisualTree(e);
    }

    private void ScrollToEnd() => Dispatcher.UIThread.Post(() => Scroller.ScrollToEnd(), DispatcherPriority.Background);

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _attached?.SendCommand.CanExecute(null) != true)
            return;
        _attached.SendCommand.Execute(null);
        e.Handled = true;
    }
}
