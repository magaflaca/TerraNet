using TerraNet.Core.Localization;
using TerraNet.Core.Settings;
using TerraNet.Core.Worlds;

namespace TerraNet.App.ViewModels;

public sealed record Choice(object Value, string Label, string Sprite, double Unit = 3);

public static class Choices
{
    private static IReadOnlyList<Choice>? _pvpmodes;
    private static IReadOnlyList<Choice>? _themes;
    private static IReadOnlyList<Choice>? _evils;
    private static IReadOnlyList<Choice>? _difficulties;
    private static IReadOnlyList<Choice>? _sizes;
    public static IReadOnlyList<Choice> Sizes => _sizes ??=
    [
        new(WorldSize.Small, Loc.Get("small"), "world", 2),
        new(WorldSize.Medium, Loc.Get("medium"), "world", 3),
        new(WorldSize.Large, Loc.Get("large"), "world", 4)
    ];

    public static IReadOnlyList<Choice> Difficulties => _difficulties ??=
        Enum.GetValues<WorldDifficulty>()
            .OrderBy(d => d == WorldDifficulty.Journey ? -1 : (int)d)
            .Select(d => new Choice(d, Loc.Get(d.ToString().ToLowerInvariant()), DifficultySprite(d)))
            .ToList();

    public static IReadOnlyList<Choice> Evils => _evils ??=
        Enum.GetValues<WorldEvil>()
            .Select(e => new Choice(e, Loc.Get(e.ToString().ToLowerInvariant()), EvilSprite(e)))
            .ToList();

    public static IReadOnlyList<Choice> Themes => _themes ??=
    [
        new(AppTheme.Auto, Loc.Get("themeAuto"), "globe"),
        new(AppTheme.Light, Loc.Get("themeLight"), "sunIcon"),
        new(AppTheme.Dark, Loc.Get("themeDark"), "moonIcon")
    ];

    public static IReadOnlyList<Choice> PvpModes => _pvpmodes ??=
    [
        new(PvpMode.Normal, Loc.Get("pvpNormal"), "heart"),
        new(PvpMode.Always, Loc.Get("pvpAlways"), "flame"),
        new(PvpMode.Disabled, Loc.Get("pvpDisabled"), "lock")
    ];

    public static IReadOnlyList<Choice> Languages =>
    [
        new("auto", Loc.Get("themeAuto"), "globe"),
        new("en", "English", "sparkle"),
        new("es", "Español", "sparkle")
    ];

    public static void Reset()
    {
        _sizes = null;
        _difficulties = null;
        _evils = null;
        _themes = null;
        _pvpmodes = null;
    }

    public static Choice Find(IReadOnlyList<Choice> choices, object value) =>
        choices.FirstOrDefault(c => c.Value.Equals(value)) ?? choices[0];

    public static string DifficultySprite(WorldDifficulty difficulty) => difficulty switch
    {
        WorldDifficulty.Journey => "sprout",
        WorldDifficulty.Expert => "flame",
        WorldDifficulty.Master => "crown",
        _ => "heart"
    };

    public static string EvilSprite(WorldEvil evil) => evil switch
    {
        WorldEvil.Corruption => "crystal",
        WorldEvil.Crimson => "blood",
        _ => "dice"
    };
}
