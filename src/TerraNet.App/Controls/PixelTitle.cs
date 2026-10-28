using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TerraNet.App.Controls;

public sealed class PixelTitle : Control
{
    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<PixelTitle, double>(nameof(FontSize), 36);

    public static readonly StyledProperty<double> UnitProperty =
        AvaloniaProperty.Register<PixelTitle, double>(nameof(Unit), 3);

    private static readonly (string Text, IBrush Fill, IBrush Shine)[] Parts =
    [
        ("Terra", new SolidColorBrush(Color.Parse("#7BD86F")), new SolidColorBrush(Color.Parse("#C4F5A8"))),
        ("Net", new SolidColorBrush(Color.Parse("#FFCF5C")), new SolidColorBrush(Color.Parse("#FFF0B8")))
    ];

    private static readonly IBrush Outline = new SolidColorBrush(Color.Parse("#3B2F4A"));

    static PixelTitle()
    {
        AffectsMeasure<PixelTitle>(FontSizeProperty, UnitProperty);
        AffectsRender<PixelTitle>(FontSizeProperty, UnitProperty);
    }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public double Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }

    private IEnumerable<(Geometry Geometry, IBrush Fill, IBrush Shine)> Build()
    {
        var family = this.TryFindResource("PixelFont", out var resource) && resource is FontFamily font ? font : FontFamily.Default;
        var typeface = new Typeface(family, FontStyle.Normal, FontWeight.Bold);
        var x = Unit;
        foreach (var (text, fill, shine) in Parts)
        {
            var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, FontSize, fill);
            var geometry = formatted.BuildGeometry(new Point(x, Unit));
            if (geometry is not null)
                yield return (geometry, fill, shine);
            x += formatted.WidthIncludingTrailingWhitespace;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var bounds = Build().Select(p => p.Geometry.Bounds).Aggregate(default(Rect), (a, b) => a == default ? b : a.Union(b));
        return new Size(bounds.Right + Unit * 2, bounds.Bottom + Unit * 3);
    }

    public override void Render(DrawingContext context)
    {
        var u = Unit;
        var parts = Build().ToList();
        var offsets = new List<Vector>();
        for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 2; dy++)
                offsets.Add(new Vector(dx * u, dy * u));

        foreach (var offset in offsets)
            using (context.PushTransform(Matrix.CreateTranslation(offset)))
                foreach (var part in parts)
                    context.DrawGeometry(Outline, null, part.Geometry);

        foreach (var part in parts)
        {
            context.DrawGeometry(part.Fill, null, part.Geometry);
            using (context.PushClip(new Rect(0, 0, Bounds.Width, part.Geometry.Bounds.Top + part.Geometry.Bounds.Height * 0.38)))
                context.DrawGeometry(part.Shine, null, part.Geometry);
        }
    }
}
