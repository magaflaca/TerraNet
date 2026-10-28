using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TerraNet.App.Controls;

public sealed class PixelFrame : Decorator, Avalonia.Rendering.ICustomHitTest
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<PixelFrame, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<PixelFrame, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<IBrush?> ShadowProperty =
        AvaloniaProperty.Register<PixelFrame, IBrush?>(nameof(Shadow));

    public static readonly StyledProperty<IBrush?> HighlightProperty =
        AvaloniaProperty.Register<PixelFrame, IBrush?>(nameof(Highlight));

    public static readonly StyledProperty<double> UnitProperty =
        AvaloniaProperty.Register<PixelFrame, double>(nameof(Unit), 3);

    public static readonly StyledProperty<int> DepthProperty =
        AvaloniaProperty.Register<PixelFrame, int>(nameof(Depth), 1);

    public static readonly StyledProperty<bool> SunkProperty =
        AvaloniaProperty.Register<PixelFrame, bool>(nameof(Sunk));

    static PixelFrame()
    {
        AffectsRender<PixelFrame>(FillProperty, StrokeProperty, ShadowProperty, HighlightProperty, UnitProperty, DepthProperty, SunkProperty);
        AffectsArrange<PixelFrame>(SunkProperty);
        AffectsMeasure<PixelFrame>(UnitProperty, DepthProperty);
    }

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public IBrush? Shadow { get => GetValue(ShadowProperty); set => SetValue(ShadowProperty, value); }
    public IBrush? Highlight { get => GetValue(HighlightProperty); set => SetValue(HighlightProperty, value); }
    public double Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public int Depth { get => GetValue(DepthProperty); set => SetValue(DepthProperty, value); }
    public bool Sunk { get => GetValue(SunkProperty); set => SetValue(SunkProperty, value); }

    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    private double Drop => Sunk ? Unit * Depth : 0;

    private Thickness Chrome => new(Unit, Unit, Unit, Unit * (1 + Depth));

    protected override Size MeasureOverride(Size availableSize)
    {
        var chrome = Chrome + Padding;
        if (Child is null)
            return new Size(chrome.Left + chrome.Right, chrome.Top + chrome.Bottom);
        Child.Measure(availableSize.Deflate(chrome));
        return Child.DesiredSize.Inflate(chrome);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(finalSize).Deflate(Chrome + Padding).Translate(new Vector(0, Drop)));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var u = Unit;
        var body = new Rect(0, Drop, Bounds.Width, Bounds.Height - u * Depth);
        if (body.Width < u * 4 || body.Height < u * 4)
            return;

        if (Shadow is { } shadow && Depth > 0 && !Sunk)
            context.DrawGeometry(shadow, null, Notched(body.Translate(new Vector(0, u * Depth)), u));
        if (Stroke is { } stroke)
            context.DrawGeometry(stroke, null, Notched(body, u));
        if (Fill is { } fill)
            context.DrawGeometry(fill, null, Notched(body.Deflate(u), u));
        if (Highlight is { } highlight)
            context.FillRectangle(highlight, new Rect(body.X + u * 2, body.Y + u, body.Width - u * 4, u));
    }

    private static StreamGeometry Notched(Rect r, double u)
    {
        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        g.BeginFigure(new Point(r.Left + u, r.Top), true);
        g.LineTo(new Point(r.Right - u, r.Top));
        g.LineTo(new Point(r.Right - u, r.Top + u));
        g.LineTo(new Point(r.Right, r.Top + u));
        g.LineTo(new Point(r.Right, r.Bottom - u));
        g.LineTo(new Point(r.Right - u, r.Bottom - u));
        g.LineTo(new Point(r.Right - u, r.Bottom));
        g.LineTo(new Point(r.Left + u, r.Bottom));
        g.LineTo(new Point(r.Left + u, r.Bottom - u));
        g.LineTo(new Point(r.Left, r.Bottom - u));
        g.LineTo(new Point(r.Left, r.Top + u));
        g.LineTo(new Point(r.Left + u, r.Top + u));
        g.EndFigure(true);
        return geometry;
    }
}
