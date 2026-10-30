using Avalonia;
using Avalonia.Controls;

namespace TerraNet.App.Views;

public partial class PageHeader : UserControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<PageHeader, string?>(nameof(Title));

    public PageHeader()
    {
        InitializeComponent();
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
}
