using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TerraNet.App.Controls;

public sealed partial class ChatPreview : TextBlock
{
    public static readonly StyledProperty<string?> MarkupProperty =
        AvaloniaProperty.Register<ChatPreview, string?>(nameof(Markup));

    static ChatPreview()
    {
        MarkupProperty.Changed.AddClassHandler<ChatPreview>((preview, _) => preview.Rebuild());
    }

    public string? Markup
    {
        get => GetValue(MarkupProperty);
        set => SetValue(MarkupProperty, value);
    }

    [GeneratedRegex(@"\[c/([0-9a-fA-F]{6}):([^\]]*)\]")]
    private static partial Regex ColorTag();

    private void Rebuild()
    {
        var inlines = new InlineCollection();
        var text = Markup ?? "";
        var position = 0;
        foreach (Match match in ColorTag().Matches(text))
        {
            if (match.Index > position)
                inlines.Add(new Run(text[position..match.Index]));
            inlines.Add(new Run(match.Groups[2].Value) { Foreground = new SolidColorBrush(Color.Parse("#" + match.Groups[1].Value)) });
            position = match.Index + match.Length;
        }
        if (position < text.Length)
            inlines.Add(new Run(text[position..]));
        Inlines = inlines;
    }
}
