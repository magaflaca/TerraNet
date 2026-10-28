using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TerraNet.App.Controls;

public sealed class PixelProgress : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<PixelProgress, double>(nameof(Value));

    public static readonly StyledProperty<bool> IsIndeterminateProperty =
        AvaloniaProperty.Register<PixelProgress, bool>(nameof(IsIndeterminate));

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<PixelProgress, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> TrackProperty =
        AvaloniaProperty.Register<PixelProgress, IBrush?>(nameof(Track));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<PixelProgress, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<double> UnitProperty =
        AvaloniaProperty.Register<PixelProgress, double>(nameof(Unit), 3);

    private static readonly IBrush Shine = new SolidColorBrush(Colors.White, 0.45);

    private TimeSpan _time;
    private bool _animating;

    static PixelProgress()
    {
        AffectsRender<PixelProgress>(ValueProperty, IsIndeterminateProperty, FillProperty, TrackProperty, StrokeProperty, UnitProperty);
    }

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public bool IsIndeterminate { get => GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public IBrush? Track { get => GetValue(TrackProperty); set => SetValue(TrackProperty, value); }
    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public double Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 120 : availableSize.Width, Unit * 6);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _animating = true;
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _animating = false;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnFrame(TimeSpan time)
    {
        if (!_animating)
            return;
        _time = time;
        InvalidateVisual();
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);
    }

    public override void Render(DrawingContext context)
    {
        var u = Unit;
        var width = Math.Floor(Bounds.Width / u) * u;
        var bar = new Rect(0, 0, width, u * 6);
        if (Stroke is { } stroke)
        {
            context.FillRectangle(stroke, new Rect(bar.X + u, bar.Y, bar.Width - u * 2, bar.Height));
            context.FillRectangle(stroke, new Rect(bar.X, bar.Y + u, bar.Width, bar.Height - u * 2));
        }
        var inner = bar.Deflate(u);
        if (Track is { } track)
            context.FillRectangle(track, inner);

        var blockWidth = u * 3;
        var gap = u;
        var count = (int)Math.Floor((inner.Width - gap) / (blockWidth + gap));
        if (count <= 0 || Fill is not { } fill)
            return;

        var t = _time.TotalSeconds;
        var filled = IsIndeterminate ? count : (int)Math.Round(Math.Clamp(Value, 0, 1) * count);
        var sweep = (int)(t * 12 % (count + 6)) - 3;
        for (var i = 0; i < filled; i++)
        {
            var block = new Rect(inner.X + gap + i * (blockWidth + gap), inner.Y + u, blockWidth, inner.Height - u * 2);
            var visible = !IsIndeterminate || Math.Abs(i - sweep) <= 2;
            if (!visible)
                continue;
            context.FillRectangle(fill, block);
            if (Math.Abs(i - sweep) <= 0)
                context.FillRectangle(Shine, block);
            context.FillRectangle(Shine, new Rect(block.X, block.Y, block.Width, u));
        }
    }
}
