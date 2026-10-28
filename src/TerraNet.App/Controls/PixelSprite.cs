using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TerraNet.App.Controls;

public sealed class PixelSprite : Control
{
    public static readonly StyledProperty<string?> SpriteProperty =
        AvaloniaProperty.Register<PixelSprite, string?>(nameof(Sprite));

    public static readonly StyledProperty<double> UnitProperty =
        AvaloniaProperty.Register<PixelSprite, double>(nameof(Unit), 3);

    public static readonly StyledProperty<IBrush?> TintProperty =
        AvaloniaProperty.Register<PixelSprite, IBrush?>(nameof(Tint), inherits: true);

    static PixelSprite()
    {
        AffectsMeasure<PixelSprite>(SpriteProperty, UnitProperty);
        AffectsRender<PixelSprite>(SpriteProperty, UnitProperty, TintProperty);
    }

    public string? Sprite
    {
        get => GetValue(SpriteProperty);
        set => SetValue(SpriteProperty, value);
    }

    public double Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public IBrush? Tint
    {
        get => GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        SpriteAtlas.Find(Sprite) is { } sprite ? new Size(sprite.Width * Unit, sprite.Height * Unit) : default;

    public override void Render(DrawingContext context)
    {
        if (SpriteAtlas.Find(Sprite) is not { } sprite)
            return;
        var size = new Size(sprite.Width * Unit, sprite.Height * Unit);
        var origin = new Point(Math.Round((Bounds.Width - size.Width) / 2), Math.Round((Bounds.Height - size.Height) / 2));
        SpriteAtlas.Draw(context, sprite, origin, Unit, Tint);
    }
}
