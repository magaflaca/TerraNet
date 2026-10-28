using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace TerraNet.App.Controls;

public sealed class PixelSwitch : ToggleButton
{
    public static readonly StyledProperty<IBrush?> OnBrushProperty =
        AvaloniaProperty.Register<PixelSwitch, IBrush?>(nameof(OnBrush));

    public static readonly StyledProperty<IBrush?> OffBrushProperty =
        AvaloniaProperty.Register<PixelSwitch, IBrush?>(nameof(OffBrush));

    public static readonly StyledProperty<IBrush?> KnobBrushProperty =
        AvaloniaProperty.Register<PixelSwitch, IBrush?>(nameof(KnobBrush));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<PixelSwitch, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<double> KnobProperty =
        AvaloniaProperty.Register<PixelSwitch, double>(nameof(Knob));

    private const double Unit = 3;
    private const int TrackUnits = 14;
    private const int HeightUnits = 8;
    private const int KnobUnits = 6;

    static PixelSwitch()
    {
        AffectsRender<PixelSwitch>(OnBrushProperty, OffBrushProperty, KnobBrushProperty, StrokeProperty, KnobProperty);
        IsCheckedProperty.Changed.AddClassHandler<PixelSwitch>((s, _) => s.Knob = s.IsChecked == true ? 1 : 0);
    }

    public PixelSwitch()
    {
        Template = null;
        Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
        Background = Brushes.Transparent;
        Transitions = new Transitions { new DoubleTransition { Property = KnobProperty, Duration = TimeSpan.FromMilliseconds(140), Easing = new Avalonia.Animation.Easings.CubicEaseOut() } };
    }

    public IBrush? OnBrush { get => GetValue(OnBrushProperty); set => SetValue(OnBrushProperty, value); }
    public IBrush? OffBrush { get => GetValue(OffBrushProperty); set => SetValue(OffBrushProperty, value); }
    public IBrush? KnobBrush { get => GetValue(KnobBrushProperty); set => SetValue(KnobBrushProperty, value); }
    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public double Knob { get => GetValue(KnobProperty); set => SetValue(KnobProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(TrackUnits * Unit, HeightUnits * Unit);

    public override void Render(DrawingContext context)
    {
        var u = Unit;
        var origin = new Point(Bounds.Width - TrackUnits * u, Math.Round((Bounds.Height - HeightUnits * u) / 2));
        var track = new Rect(origin, new Size(TrackUnits * u, HeightUnits * u));

        if (Stroke is { } stroke)
            Block(context, stroke, track);
        var fill = Knob > 0.5 ? OnBrush : OffBrush;
        if (fill is not null)
            Block(context, fill, track.Deflate(u));

        var travel = (TrackUnits - KnobUnits - 2) * u;
        var x = Math.Round(Knob * travel / u) * u;
        var knob = new Rect(track.X + u + x, track.Y + u, KnobUnits * u, KnobUnits * u);
        if (Stroke is { } edge)
            Block(context, edge, knob);
        if (KnobBrush is { } knobFill)
            Block(context, knobFill, knob.Deflate(u));
    }

    private static void Block(DrawingContext context, IBrush brush, Rect r)
    {
        context.FillRectangle(brush, new Rect(r.X + Unit, r.Y, r.Width - Unit * 2, r.Height));
        context.FillRectangle(brush, new Rect(r.X, r.Y + Unit, r.Width, r.Height - Unit * 2));
    }
}
