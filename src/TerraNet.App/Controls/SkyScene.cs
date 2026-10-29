using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace TerraNet.App.Controls;

public enum SceneMood
{
    Idle,
    Busy,
    Online
}

public sealed class SkyScene : Control
{
    public static readonly StyledProperty<SceneMood> MoodProperty =
        AvaloniaProperty.Register<SkyScene, SceneMood>(nameof(Mood));

    public static readonly StyledProperty<Color> SkyTopProperty =
        AvaloniaProperty.Register<SkyScene, Color>(nameof(SkyTop), Color.Parse("#8FD0FF"));

    public static readonly StyledProperty<Color> SkyBottomProperty =
        AvaloniaProperty.Register<SkyScene, Color>(nameof(SkyBottom), Color.Parse("#EAF7FF"));

    private const int SkyBands = 7;
    private static readonly (double X, double Y, double Speed, string Sprite)[] Clouds =
    [
        (0.05, 0.16, 0.010, "cloudSmall"),
        (0.55, 0.10, 0.008, "cloudSmall"),
        (0.30, 0.30, 0.018, "cloud"),
        (0.82, 0.38, 0.022, "cloud")
    ];
    private static readonly (double X, double Y, double Phase)[] Stars = Enumerable.Range(0, 22)
        .Select(i => (Hash(i * 3 + 1), Hash(i * 3 + 2) * 0.6, Hash(i * 3 + 3) * Math.Tau))
        .ToArray();
    private static readonly (double X, string Sprite)[] Decorations =
    [
        (0.08, "tuft"), (0.17, "flower"), (0.41, "tuft"), (0.58, "flower"), (0.66, "tuft"), (0.79, "flower"), (0.93, "tuft")
    ];

    private TimeSpan _time;
    private bool _animating;

    static SkyScene()
    {
        AffectsRender<SkyScene>(MoodProperty, SkyTopProperty, SkyBottomProperty);
    }

    public SkyScene()
    {
        ClipToBounds = true;
    }

    public SceneMood Mood { get => GetValue(MoodProperty); set => SetValue(MoodProperty, value); }
    public Color SkyTop { get => GetValue(SkyTopProperty); set => SetValue(SkyTopProperty, value); }
    public Color SkyBottom { get => GetValue(SkyBottomProperty); set => SetValue(SkyBottomProperty, value); }

    private bool IsNight => ActualThemeVariant == ThemeVariant.Dark;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _animating = true;
        RequestFrame();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _animating = false;
        base.OnDetachedFromVisualTree(e);
    }

    private void RequestFrame() => TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);

    private void OnFrame(TimeSpan time)
    {
        if (!_animating)
            return;
        _time = time;
        InvalidateVisual();
        RequestFrame();
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0)
            return;

        var unit = Math.Clamp(Math.Round(Math.Min(width / 2, height) / 60), 2, 6);
        var t = _time.TotalSeconds;
        var groundTop = height - unit * 9;

        DrawSky(context, width, groundTop);
        if (IsNight)
            DrawStars(context, width, groundTop, unit, t);
        DrawCelestial(context, width, unit, t);
        DrawClouds(context, width, groundTop, unit, t);
        DrawGround(context, width, groundTop, unit);
        DrawMascot(context, width, groundTop, unit, t);
    }

    private void DrawSky(DrawingContext context, double width, double groundTop)
    {
        var band = Math.Ceiling(groundTop / SkyBands);
        for (var i = 0; i < SkyBands; i++)
        {
            var color = Lerp(SkyTop, SkyBottom, i / (double)(SkyBands - 1));
            context.FillRectangle(new SolidColorBrush(color), new Rect(0, i * band, width, band + 1));
        }
    }

    private static void DrawStars(DrawingContext context, double width, double groundTop, double unit, double t)
    {
        if (SpriteAtlas.Find("twinkle") is not { } star)
            return;
        foreach (var (x, y, phase) in Stars)
        {
            var glow = 0.35 + 0.65 * Math.Max(0, Math.Sin(t * 1.3 + phase));
            SpriteAtlas.Draw(context, star, Snap(new Point(x * width, y * groundTop), unit), unit, null, glow);
        }
    }

    private void DrawCelestial(DrawingContext context, double width, double unit, double t)
    {
        if (SpriteAtlas.Find(IsNight ? "moon" : "sun") is not { } body)
            return;
        var bob = Math.Round(Math.Sin(t * 0.6) * 1.5) * unit;
        SpriteAtlas.Draw(context, body, Snap(new Point(width - body.Width * unit - unit * 8, unit * 5 + bob), unit), unit, null);
    }

    private void DrawClouds(DrawingContext context, double width, double groundTop, double unit, double t)
    {
        var opacity = IsNight ? 0.28 : 0.95;
        foreach (var (x, y, speed, sprite) in Clouds)
        {
            if (SpriteAtlas.Find(sprite) is not { } cloud)
                continue;
            var span = width + cloud.Width * unit;
            var position = ((x + t * speed) % 1.0) * span - cloud.Width * unit;
            SpriteAtlas.Draw(context, cloud, Snap(new Point(position, y * groundTop), unit), unit, null, opacity);
        }
    }

    private void DrawGround(DrawingContext context, double width, double groundTop, double unit)
    {
        if (SpriteAtlas.Find(IsNight ? "grassNight" : "grass") is not { } tile)
            return;
        var step = tile.Width * unit;
        for (var x = 0.0; x < width; x += step)
            SpriteAtlas.Draw(context, tile, new Point(x, groundTop), unit, null);

        foreach (var (x, name) in Decorations)
            if (SpriteAtlas.Find(name) is { } decoration)
                SpriteAtlas.Draw(context, decoration, Snap(new Point(x * width, groundTop - decoration.Height * unit), unit), unit, null);
    }

    private void DrawMascot(DrawingContext context, double width, double groundTop, double unit, double t)
    {
        var (period, height) = Mood switch
        {
            SceneMood.Busy => (0.9, 3.0),
            SceneMood.Online => (1.6, 6.0),
            _ => (3.2, 4.0)
        };
        var phase = t % period / period;
        var hopping = phase < 0.35;
        var lift = hopping ? Math.Sin(phase / 0.35 * Math.PI) * height : 0;
        var squash = hopping ? 1.0 : 1 - 0.08 * Math.Max(0, Math.Sin((phase - 0.35) / 0.65 * Math.PI * 2));
        var blink = t % 4.7 < 0.14;

        if (SpriteAtlas.Find(blink ? "slimeBlink" : "slime") is not { } slime)
            return;

        var groundUnit = unit;
        var tall = groundTop > 380;
        unit = tall ? unit * 2 : unit;

        var spriteWidth = slime.Width * unit;
        var spriteHeight = slime.Height * unit;
        var baseX = Math.Round((tall ? (width - spriteWidth) / 2 : width * 0.2) / unit) * unit;
        var baseY = groundTop - spriteHeight + groundUnit * 2;
        var origin = new Point(baseX, baseY - Math.Round(lift) * groundUnit);

        var pivot = new Point(origin.X + spriteWidth / 2, origin.Y + spriteHeight);
        var transform = Matrix.CreateTranslation(-pivot.X, -pivot.Y)
                        * Matrix.CreateScale(2 - squash, squash)
                        * Matrix.CreateTranslation(pivot.X, pivot.Y);
        using (context.PushTransform(transform))
            SpriteAtlas.Draw(context, slime, origin, unit, null);

        if (Mood == SceneMood.Online && SpriteAtlas.Find("sparkle") is { } sparkle)
        {
            for (var i = 0; i < 3; i++)
            {
                var angle = t * 1.4 + i * Math.Tau / 3;
                var point = new Point(pivot.X + Math.Cos(angle) * spriteWidth * 0.9, origin.Y + spriteHeight * 0.3 + Math.Sin(angle) * spriteHeight * 0.6);
                SpriteAtlas.Draw(context, sparkle, Snap(point, groundUnit), groundUnit, null, 0.6 + 0.4 * Math.Sin(t * 3 + i));
            }
        }
    }

    private static Point Snap(Point point, double unit) =>
        new(Math.Round(point.X / unit) * unit, Math.Round(point.Y / unit) * unit);

    private static Color Lerp(Color a, Color b, double f) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * f),
        (byte)(a.G + (b.G - a.G) * f),
        (byte)(a.B + (b.B - a.B) * f));

    private static double Hash(int seed)
    {
        var x = Math.Sin(seed * 12.9898) * 43758.5453;
        return x - Math.Floor(x);
    }
}
