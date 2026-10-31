using Avalonia;
using Avalonia.Controls;

namespace TerraNet.App.Views;

public partial class HomeView : UserControl
{
    private const double WideBreakpoint = 820;
    private const double NarrowHeroHeight = 230;
    private bool? _wide;

    public HomeView()
    {
        InitializeComponent();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var wide = e.NewSize.Width >= WideBreakpoint;
        if (wide == _wide)
            return;
        _wide = wide;

        Root.RowDefinitions = wide ? new RowDefinitions("*") : new RowDefinitions("Auto,*");
        Root.ColumnDefinitions = wide ? new ColumnDefinitions("5*,6*") : new ColumnDefinitions("*");
        Grid.SetRow(Body, wide ? 0 : 1);
        Grid.SetColumn(Body, wide ? 1 : 0);
        Hero.Height = wide ? double.NaN : NarrowHeroHeight;
        Title.FontSize = wide ? 52 : 38;
        Stack.Margin = wide ? new Thickness(28, 28, 28, 32) : new Thickness(16, 18, 16, 28);
    }
}
