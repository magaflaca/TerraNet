using System.Text.Json;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TerraNet.App.Controls;

public sealed record SpriteRun(char Key, int X, int Y, int Length);

public sealed class SpriteData(string name, int width, int height, IReadOnlyList<SpriteRun> runs)
{
    public string Name { get; } = name;
    public int Width { get; } = width;
    public int Height { get; } = height;
    public IReadOnlyList<SpriteRun> Runs { get; } = runs;
}

public static class SpriteAtlas
{
    public const char TintKey = '#';

    private static readonly Lazy<(Dictionary<string, SpriteData> Sprites, Dictionary<char, IImmutableSolidColorBrush> Palette)> Data = new(Load);

    public static SpriteData? Find(string? name) =>
        name is not null && Data.Value.Sprites.TryGetValue(name, out var sprite) ? sprite : null;

    public static IBrush? Color(char key) => Data.Value.Palette.GetValueOrDefault(key);

    public static void Draw(DrawingContext context, SpriteData sprite, Point origin, double unit, IBrush? tint, double opacity = 1)
    {
        if (opacity >= 1)
        {
            DrawRuns(context, sprite, origin, unit, tint);
            return;
        }
        using (context.PushOpacity(opacity))
            DrawRuns(context, sprite, origin, unit, tint);
    }

    private static void DrawRuns(DrawingContext context, SpriteData sprite, Point origin, double unit, IBrush? tint)
    {
        foreach (var run in sprite.Runs)
        {
            var brush = run.Key == TintKey ? tint : Color(run.Key);
            if (brush is null)
                continue;
            context.FillRectangle(brush, new Rect(origin.X + run.X * unit, origin.Y + run.Y * unit, run.Length * unit + 0.5, unit + 0.5));
        }
    }

    private static (Dictionary<string, SpriteData>, Dictionary<char, IImmutableSolidColorBrush>) Load()
    {
        using var stream = typeof(SpriteAtlas).Assembly.GetManifestResourceStream("TerraNet.Sprites.json")
                           ?? throw new InvalidOperationException("sprite atlas missing");
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;

        var palette = root.GetProperty("palette").EnumerateObject()
            .ToDictionary(p => p.Name[0], p => (IImmutableSolidColorBrush)new ImmutableSolidColorBrush(Avalonia.Media.Color.Parse(p.Value.GetString()!)));

        var sprites = new Dictionary<string, SpriteData>();
        foreach (var group in new[] { "sprites", "glyphs" })
            foreach (var entry in root.GetProperty(group).EnumerateObject())
                sprites[entry.Name] = Parse(entry.Name, entry.Value.EnumerateArray().Select(r => r.GetString()!).ToList());

        return (sprites, palette);
    }

    private static SpriteData Parse(string name, IReadOnlyList<string> rows)
    {
        var runs = new List<SpriteRun>();
        for (var y = 0; y < rows.Count; y++)
        {
            var row = rows[y];
            var x = 0;
            while (x < row.Length)
            {
                var key = row[x];
                var start = x;
                while (x < row.Length && row[x] == key)
                    x++;
                if (key != '.')
                    runs.Add(new SpriteRun(key, start, y, x - start));
            }
        }
        return new SpriteData(name, rows.Max(r => r.Length), rows.Count, runs);
    }
}
