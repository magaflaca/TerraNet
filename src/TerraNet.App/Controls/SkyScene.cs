using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;

namespace TerraNet.App.Controls;

public enum SceneMood
{
    Idle,
    Busy,
    Online
}

public sealed class SkyScene : Control, Avalonia.Rendering.ICustomHitTest
{
    public static readonly StyledProperty<SceneMood> MoodProperty =
        AvaloniaProperty.Register<SkyScene, SceneMood>(nameof(Mood));

    public static readonly StyledProperty<double> HorizonProperty =
        AvaloniaProperty.Register<SkyScene, double>(nameof(Horizon), 0.82);

    public static readonly StyledProperty<double> FocusXProperty =
        AvaloniaProperty.Register<SkyScene, double>(nameof(FocusX), 0.3);

    private const int SkyBands = 8;

    private static readonly (double X, double Y, double Speed, string Sprite)[] Clouds =
    [
        (0.02, 0.12, 0.006, "cloudSmall"),
        (0.47, 0.07, 0.005, "cloudSmall"),
        (0.71, 0.22, 0.008, "cloudSmall"),
        (0.25, 0.30, 0.012, "cloud"),
        (0.86, 0.40, 0.015, "cloud")
    ];

    private static readonly (double X, string Sprite)[] Props =
    [
        (0.47, "tree"), (0.55, "bush"), (0.61, "mushroom"), (0.69, "treeBig"), (0.78, "bush"), (0.86, "tree"), (0.93, "mushroom"), (0.98, "treeBig")
    ];

    private static readonly double[] Flowers = [0.03, 0.09, 0.21, 0.38, 0.44, 0.52, 0.58, 0.66, 0.74, 0.81, 0.9, 0.96];

    private RenderTargetBitmap? _backdrop;
    private (Size Size, double Scale, bool Night, double Horizon, double Focus) _backdropKey;
    private TimeSpan _time;
    private bool _animating;
    private int _frame;

    static SkyScene()
    {
        AffectsRender<SkyScene>(MoodProperty, HorizonProperty, FocusXProperty);
    }

    public SkyScene()
    {
        ClipToBounds = true;
    }

    public SceneMood Mood { get => GetValue(MoodProperty); set => SetValue(MoodProperty, value); }
    public double Horizon { get => GetValue(HorizonProperty); set => SetValue(HorizonProperty, value); }
    public double FocusX { get => GetValue(FocusXProperty); set => SetValue(FocusXProperty, value); }

    private bool IsNight => ActualThemeVariant == ThemeVariant.Dark;

    private Rect _celestial;

    public event Action<bool>? CelestialTapped;

    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    protected override void OnPointerPressed(Avalonia.Input.PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!_celestial.Contains(e.GetPosition(this)))
            return;
        e.Handled = true;
        CelestialTapped?.Invoke(IsNight);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _animating = true;
        RequestFrame();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _animating = false;
        _backdrop?.Dispose();
        _backdrop = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void RequestFrame() => TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);

    private void OnFrame(TimeSpan time)
    {
        if (!_animating)
            return;
        _time = time;
        if (++_frame % 3 == 0 && IsEffectivelyVisible)
            InvalidateVisual();
        RequestFrame();
    }

    private SceneGeometry Geometry()
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var unit = Math.Round(Math.Clamp(Math.Round(Math.Min(width, height) / 170), 2, 4) * scale) / scale;
        var horizon = Math.Round(height * Horizon / unit) * unit;
        return new SceneGeometry(width, height, unit, horizon, Math.Round(width * FocusX / unit) * unit);
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
            return;
        var g = Geometry();
        var t = _time.TotalSeconds;

        var backdrop = Backdrop(g);
        context.DrawImage(backdrop, new Rect(0, 0, backdrop.PixelSize.Width, backdrop.PixelSize.Height), new Rect(Bounds.Size));
        if (IsNight)
            DrawStars(context, g, t);
        DrawCelestial(context, g, t);
        DrawClouds(context, g, t);
        DrawIsland(context, g, t);
        DrawBirds(context, g, t);
        DrawCritters(context, g, t);
        DrawMascot(context, g, t);
    }

    private RenderTargetBitmap Backdrop(SceneGeometry g)
    {
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var key = (Bounds.Size, scale, IsNight, Horizon, FocusX);
        if (_backdrop is not null && key == _backdropKey)
            return _backdrop;

        _backdrop?.Dispose();
        var pixels = new PixelSize(Math.Max(1, (int)Math.Ceiling(g.Width * scale)), Math.Max(1, (int)Math.Ceiling(g.Height * scale)));
        _backdrop = new RenderTargetBitmap(pixels, new Vector(96, 96));
        using (var context = _backdrop.CreateDrawingContext())
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            DrawSky(context, g);
            DrawMountains(context, g);
            DrawHills(context, g);
            DrawUnderground(context, g);
            DrawProps(context, g);
        }
        _backdropKey = key;
        return _backdrop;
    }

    private void DrawSky(DrawingContext context, SceneGeometry g)
    {
        var (top, bottom) = IsNight ? (Palette.NightTop, Palette.NightBottom) : (Palette.DayTop, Palette.DayBottom);
        var band = Math.Ceiling(g.Horizon / SkyBands / g.Unit) * g.Unit;
        for (var i = 0; i < SkyBands; i++)
            context.FillRectangle(new SolidColorBrush(Palette.Lerp(top, bottom, i / (double)(SkyBands - 1))), new Rect(0, i * band, g.Width, band + 1));
    }

    private void DrawMountains(DrawingContext context, SceneGeometry g)
    {
        var body = new SolidColorBrush(IsNight ? Palette.NightMountain : Palette.DayMountain);
        var rim = new SolidColorBrush(IsNight ? Palette.NightMountainRim : Palette.DayMountainRim);
        var snow = new SolidColorBrush(IsNight ? Palette.NightSnow : Palette.DaySnow);
        var u = g.Unit;
        var amplitude = Math.Min(g.Horizon * 0.42, 220);
        for (var x = 0.0; x < g.Width; x += u)
        {
            var n = Math.Sin(x * 0.0061 + 1.3) * 0.55 + Math.Sin(x * 0.0173 + 0.4) * 0.3 + Math.Sin(x * 0.041) * 0.08;
            var height = Math.Round(amplitude * (0.55 + 0.45 * n) / u) * u;
            var top = g.Horizon - height;
            context.FillRectangle(body, new Rect(x, top, u, height));
            context.FillRectangle(rim, new Rect(x, top, u, u));
            if (height > amplitude * 0.86)
                context.FillRectangle(snow, new Rect(x, top, u, u * (2 + Math.Round((height - amplitude * 0.86) / u / 2))));
        }
    }

    private void DrawHills(DrawingContext context, SceneGeometry g)
    {
        var body = new SolidColorBrush(IsNight ? Palette.NightHill : Palette.DayHill);
        var rim = new SolidColorBrush(IsNight ? Palette.NightHillRim : Palette.DayHillRim);
        var u = g.Unit;
        var amplitude = Math.Min(g.Horizon * 0.2, 110);
        for (var x = 0.0; x < g.Width; x += u)
        {
            var n = Math.Sin(x * 0.0093 + 2.1) * 0.6 + Math.Sin(x * 0.023 + 0.7) * 0.4;
            var height = Math.Round(amplitude * (0.5 + 0.5 * n) / u) * u + u * 2;
            context.FillRectangle(body, new Rect(x, g.Horizon - height, u, height));
            context.FillRectangle(rim, new Rect(x, g.Horizon - height, u, u));
        }
    }

    private void DrawUnderground(DrawingContext context, SceneGeometry g)
    {
        var u = g.Unit;
        if (SpriteAtlas.Find(IsNight ? "grassNight" : "grass") is not { } tile)
            return;
        var step = tile.Width * u;
        for (var x = 0.0; x < g.Width; x += step)
            SpriteAtlas.Draw(context, tile, new Point(x, g.Horizon), u, null);

        var dirtTop = g.Horizon + tile.Height * u;
        var layers = new[] { Palette.Dirt, Palette.DirtDeep, Palette.Stone, Palette.StoneDeep };
        var layerHeight = Math.Max(u * 14, Math.Round((g.Height - dirtTop) / 3.2 / u) * u);
        for (var i = 0; i < layers.Length; i++)
        {
            var color = IsNight ? Palette.Dim(layers[i]) : layers[i];
            var fill = new SolidColorBrush(color);
            var speck = new SolidColorBrush(Palette.Dim(color, 0.84));
            var light = new SolidColorBrush(Palette.Lerp(color, Colors.White, 0.18));
            var top = dirtTop + i * layerHeight;
            if (top > g.Height)
                break;
            for (var x = 0.0; x < g.Width; x += u)
            {
                var jag = i == 0 ? 0 : Math.Round((Math.Sin(x * 0.05 + i) * 1.5 + Hash((int)(x / u) + i * 977) * 2) ) * u;
                context.FillRectangle(fill, new Rect(x, top + jag, u, g.Height - top - jag + 1));
                if (i > 0)
                    context.FillRectangle(light, new Rect(x, top + jag, u, u));
            }
            var area = g.Width * layerHeight / (u * u);
            for (var s = 0; s < area / 70; s++)
            {
                var px = Math.Floor(Hash(s * 7 + i * 131) * g.Width / u) * u;
                var py = top + u * 2 + Math.Floor(Hash(s * 13 + i * 71) * layerHeight / u) * u;
                context.FillRectangle(Hash(s + i) > 0.85 ? light : speck, new Rect(px, py, u * (Hash(s * 3) > 0.7 ? 2 : 1), u));
            }
        }

        var pebble = new SolidColorBrush(IsNight ? Palette.Dim(Palette.Stone) : Palette.Stone);
        for (var i = 0; i < g.Width / (u * 40); i++)
        {
            var px = Math.Floor(Hash(i * 19 + 3) * g.Width / u) * u;
            var py = dirtTop + u * 3 + Math.Floor(Hash(i * 23 + 1) * layerHeight * 0.8 / u) * u;
            context.FillRectangle(pebble, new Rect(px, py, u * 3, u * 2));
            context.FillRectangle(pebble, new Rect(px + u, py - u, u * 2, u));
        }

        var gems = new[] { Palette.GemGold, Palette.GemBlue, Palette.GemPink, Palette.GemPurple };
        var count = (int)(g.Width * (g.Height - dirtTop) / (u * u * 700));
        for (var i = 0; i < count; i++)
        {
            var px = Math.Floor(Hash(i * 31 + 5) * g.Width / u) * u;
            var py = dirtTop + u * 8 + Math.Floor(Hash(i * 17 + 9) * (g.Height - dirtTop) / u) * u;
            var gem = new SolidColorBrush(gems[i % gems.Length]);
            context.FillRectangle(gem, new Rect(px, py, u * 3, u));
            context.FillRectangle(gem, new Rect(px + u, py - u, u, u * 3));
            context.FillRectangle(Brushes.White, new Rect(px + u, py - u, u, u));
        }
    }

    private void DrawProps(DrawingContext context, SceneGeometry g)
    {
        var u = g.Unit;
        var ground = g.Horizon + u * 2;

        void Place(string name, double x, double sink = 0)
        {
            if (SpriteAtlas.Find(name) is { } sprite)
                SpriteAtlas.Draw(context, sprite, Snap(new Point(x, ground - sprite.Height * u + sink * u), u), u, null);
        }

        foreach (var x in Flowers)
            Place(Hash((int)(x * 1000)) > 0.5 ? "flower" : "tuft", x * g.Width);

        var houseWidth = (SpriteAtlas.Find("house")?.Width ?? 24) * u;
        var houseX = g.Focus - houseWidth - u * 22;
        Place("treeBig", houseX - u * 24, 1);
        Place(IsNight ? "houseLit" : "house", houseX, 1);
        Place("bush", houseX - u * 8, 1);
        Place(IsNight ? "lampLit" : "lamp", houseX + houseWidth + u * 2, 1);

        foreach (var (x, name) in Props)
        {
            var px = g.Focus + (g.Width - g.Focus) * (x - 0.4) / 0.6 + u * 18;
            if (px > g.Focus + u * 14)
                Place(name, px, 1);
        }
        for (var x = houseX - u * 60; x > -u * 30; x -= u * 46)
            Place(Hash((int)x) > 0.5 ? "tree" : "bush", x, 1);

        if (IsNight)
        {
            var glow = new SolidColorBrush(Palette.LampGlow, 0.22);
            var lampX = houseX + houseWidth + u * 4.5;
            context.DrawEllipse(glow, null, new Point(lampX, ground - u * 10), u * 9, u * 7);
        }
    }

    private void DrawStars(DrawingContext context, SceneGeometry g, double t)
    {
        if (SpriteAtlas.Find("twinkle") is not { } star)
            return;
        for (var i = 0; i < 34; i++)
        {
            var glow = 0.3 + 0.7 * Math.Max(0, Math.Sin(t * 1.2 + Hash(i * 3 + 3) * Math.Tau));
            var point = new Point(Hash(i * 3 + 1) * g.Width, Hash(i * 3 + 2) * g.Horizon * 0.5);
            SpriteAtlas.Draw(context, star, Snap(point, g.Unit), g.Unit, null, glow);
        }
    }

    private void DrawCelestial(DrawingContext context, SceneGeometry g, double t)
    {
        if (SpriteAtlas.Find(IsNight ? "moon" : "sun") is not { } body)
            return;
        var u = g.Unit;
        var bob = Math.Round(Math.Sin(t * 0.5) * 1.5) * u;
        var x = Math.Min(g.Focus + u * 46, g.Width - body.Width * u - u * 10);
        var origin = Snap(new Point(x, g.Horizon * 0.12 + bob), u);
        _celestial = new Rect(origin, new Size(body.Width * u, body.Height * u)).Inflate(u * 4);
        SpriteAtlas.Draw(context, body, origin, u, null);
    }

    private void DrawClouds(DrawingContext context, SceneGeometry g, double t)
    {
        var opacity = IsNight ? 0.22 : 0.95;
        foreach (var (x, y, speed, name) in Clouds)
        {
            if (SpriteAtlas.Find(name) is not { } cloud)
                continue;
            var span = g.Width + cloud.Width * g.Unit * 2;
            var position = (x + t * speed) % 1.0 * span - cloud.Width * g.Unit;
            SpriteAtlas.Draw(context, cloud, Snap(new Point(position, y * g.Horizon * 0.8), g.Unit), g.Unit, null, opacity);
        }
    }

    private void DrawIsland(DrawingContext context, SceneGeometry g, double t)
    {
        var u = g.Unit;
        var width = 22;
        var x0 = Math.Round(Math.Min(g.Width - u * (width + 6), g.Focus + u * 34) / u) * u;
        var y0 = Math.Round((g.Horizon * 0.46 + Math.Sin(t * 0.7) * u * 2) / u) * u;
        if (y0 < u * 30)
            return;
        var grass = new SolidColorBrush(IsNight ? Color.Parse("#45BFA0") : Color.Parse("#5FC970"));
        var grassLight = new SolidColorBrush(IsNight ? Color.Parse("#8AE8D0") : Color.Parse("#9BE58A"));
        var dirt = new SolidColorBrush(IsNight ? Palette.Dim(Palette.Dirt) : Palette.Dirt);
        var edge = new SolidColorBrush(IsNight ? Palette.Dim(Palette.DirtDeep) : Palette.DirtDeep);
        for (var row = 0; row < 8; row++)
        {
            var inset = row < 2 ? 0 : (row - 1) * 2;
            if (inset * 2 >= width)
                break;
            var brush = row == 0 ? grassLight : row == 1 ? grass : dirt;
            context.FillRectangle(brush, new Rect(x0 + inset * u, y0 + row * u, (width - inset * 2) * u, u));
            if (row >= 2)
            {
                context.FillRectangle(edge, new Rect(x0 + inset * u, y0 + row * u, u, u));
                context.FillRectangle(edge, new Rect(x0 + (width - inset - 1) * u, y0 + row * u, u, u));
            }
        }
        if (SpriteAtlas.Find("tree") is { } tree)
            SpriteAtlas.Draw(context, tree, new Point(x0 + u * 4, y0 - tree.Height * u + u), u, null);
        if (SpriteAtlas.Find("chest") is { } chest)
            SpriteAtlas.Draw(context, chest, new Point(x0 + u * 12, y0 - chest.Height * u + u), u, null);
    }

    private void DrawBirds(DrawingContext context, SceneGeometry g, double t)
    {
        if (IsNight)
            return;
        for (var i = 0; i < 3; i++)
        {
            var cycle = (t * 0.035 + i * 0.37) % 1.0;
            var x = cycle * (g.Width + 200) - 100;
            var y = g.Horizon * (0.18 + i * 0.07) + Math.Sin(t * 1.5 + i) * g.Unit * 3;
            var flap = (int)(t * 5 + i) % 2 == 0 ? "bird" : "birdUp";
            if (SpriteAtlas.Find(flap) is { } bird)
                SpriteAtlas.Draw(context, bird, Snap(new Point(x, y), g.Unit), g.Unit, null, 0.75);
        }
    }

    private void DrawCritters(DrawingContext context, SceneGeometry g, double t)
    {
        var u = g.Unit;
        var ground = g.Horizon + u * 2;

        if (SpriteAtlas.Find("bunny") is { } bunny)
        {
            var range = u * 70;
            var phase = t * 0.08 % 2;
            var forward = phase < 1;
            var travel = forward ? phase : 2 - phase;
            var hop = Math.Abs(Math.Sin(t * 6)) * u * 3;
            var x = g.Focus + u * 22 + travel * range;
            var origin = Snap(new Point(x, ground - bunny.Height * u - hop + u), u);
            var flip = !forward ? Matrix.Identity : Matrix.CreateTranslation(-origin.X - bunny.Width * u / 2, 0) * Matrix.CreateScale(-1, 1) * Matrix.CreateTranslation(origin.X + bunny.Width * u / 2, 0);
            using (context.PushTransform(flip))
                SpriteAtlas.Draw(context, bunny, origin, u, null);
        }

        if (IsNight)
        {
            var firefly = new SolidColorBrush(Palette.Firefly);
            var halo = new SolidColorBrush(Palette.Firefly, 0.25);
            for (var i = 0; i < 9; i++)
            {
                var x = Hash(i * 11 + 2) * g.Width + Math.Sin(t * 0.6 + i) * u * 12;
                var y = ground - u * (6 + Hash(i * 5 + 1) * 34) + Math.Cos(t * 0.8 + i * 2) * u * 5;
                var alpha = 0.4 + 0.6 * Math.Max(0, Math.Sin(t * 2 + i));
                using (context.PushOpacity(alpha))
                {
                    var p = Snap(new Point(x, y), u);
                    context.FillRectangle(halo, new Rect(p.X - u, p.Y - u, u * 3, u * 3));
                    context.FillRectangle(firefly, new Rect(p.X, p.Y, u, u));
                }
            }
            return;
        }

        for (var i = 0; i < 3; i++)
        {
            var name = (int)(t * 6 + i) % 2 == 0 ? "butterfly" : "butterflyUp";
            if (SpriteAtlas.Find(name) is not { } butterfly)
                continue;
            var cx = g.Focus + (i - 1) * u * 50 + Math.Sin(t * 0.4 + i * 2) * u * 30;
            var cy = ground - u * (16 + i * 6) + Math.Sin(t * 1.3 + i) * u * 6;
            SpriteAtlas.Draw(context, butterfly, Snap(new Point(cx, cy), u), u, null);
        }
    }

    private void DrawMascot(DrawingContext context, SceneGeometry g, double t)
    {
        var u = g.Unit;
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

        var spriteWidth = slime.Width * u;
        var spriteHeight = slime.Height * u;
        var origin = new Point(g.Focus - spriteWidth / 2, g.Horizon - spriteHeight + u * 3 - Math.Round(lift) * u);
        var pivot = new Point(origin.X + spriteWidth / 2, origin.Y + spriteHeight);
        var transform = Matrix.CreateTranslation(-pivot.X, -pivot.Y) * Matrix.CreateScale(2 - squash, squash) * Matrix.CreateTranslation(pivot.X, pivot.Y);
        using (context.PushTransform(transform))
            SpriteAtlas.Draw(context, slime, Snap(origin, u), u, null);

        if (Mood != SceneMood.Online)
            return;
        var arc = new SolidColorBrush(Palette.ArcGlow);
        for (var ring = 0; ring < 3; ring++)
        {
            var pulse = (t * 0.9 + ring / 3.0) % 1.0;
            var radius = (3 + ring * 3 + pulse * 2) * u;
            using (context.PushOpacity(1 - pulse))
                for (var a = -0.75; a <= 0.75; a += 0.09)
                {
                    var p = new Point(pivot.X + Math.Sin(a) * radius, origin.Y - u * 2 - Math.Cos(a) * radius + u * 3);
                    context.FillRectangle(arc, new Rect(Snap(p, u), new Size(u, u)));
                }
        }
        if (SpriteAtlas.Find("sparkle") is { } sparkle)
            for (var i = 0; i < 3; i++)
            {
                var angle = t * 1.4 + i * Math.Tau / 3;
                var point = new Point(pivot.X + Math.Cos(angle) * spriteWidth, origin.Y + spriteHeight * 0.4 + Math.Sin(angle) * spriteHeight * 0.5);
                SpriteAtlas.Draw(context, sparkle, Snap(point, u), u, null, 0.6 + 0.4 * Math.Sin(t * 3 + i));
            }
    }

    private static Point Snap(Point point, double unit) =>
        new(Math.Round(point.X / unit) * unit, Math.Round(point.Y / unit) * unit);

    private static double Hash(int seed)
    {
        var x = Math.Sin(seed * 12.9898 + 78.233) * 43758.5453;
        return x - Math.Floor(x);
    }

    private readonly record struct SceneGeometry(double Width, double Height, double Unit, double Horizon, double Focus);

    private static class Palette
    {
        public static readonly Color DayTop = Color.Parse("#86CBFF");
        public static readonly Color DayBottom = Color.Parse("#E4F6FF");
        public static readonly Color NightTop = Color.Parse("#141130");
        public static readonly Color NightBottom = Color.Parse("#4A3F86");
        public static readonly Color DayMountain = Color.Parse("#B7D3F4");
        public static readonly Color DayMountainRim = Color.Parse("#D3E5FA");
        public static readonly Color DaySnow = Color.Parse("#FFFFFF");
        public static readonly Color NightMountain = Color.Parse("#2D2860");
        public static readonly Color NightMountainRim = Color.Parse("#3E377A");
        public static readonly Color NightSnow = Color.Parse("#8E86C7");
        public static readonly Color DayHill = Color.Parse("#A6E3A0");
        public static readonly Color DayHillRim = Color.Parse("#C6F0BC");
        public static readonly Color NightHill = Color.Parse("#25514C");
        public static readonly Color NightHillRim = Color.Parse("#2F6A61");
        public static readonly Color Dirt = Color.Parse("#C08A5E");
        public static readonly Color DirtDeep = Color.Parse("#A3714C");
        public static readonly Color Stone = Color.Parse("#8F86A3");
        public static readonly Color StoneDeep = Color.Parse("#6F6688");
        public static readonly Color GemGold = Color.Parse("#FFD56B");
        public static readonly Color GemBlue = Color.Parse("#7CC4F7");
        public static readonly Color GemPink = Color.Parse("#FF8AAE");
        public static readonly Color GemPurple = Color.Parse("#B98BFF");
        public static readonly Color LampGlow = Color.Parse("#FFE38A");
        public static readonly Color Firefly = Color.Parse("#FFF2A0");
        public static readonly Color ArcGlow = Color.Parse("#FFE38A");

        public static Color Lerp(Color a, Color b, double f) => Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * f), (byte)(a.G + (b.G - a.G) * f), (byte)(a.B + (b.B - a.B) * f));

        public static Color Dim(Color c, double f = 0.55) =>
            Color.FromRgb((byte)(c.R * f), (byte)(c.G * f), (byte)Math.Min(255, c.B * f * 1.15));
    }
}
