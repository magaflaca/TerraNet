using TerraNet.Core.Localization;
using TerraNet.Core.Settings;
using TerraNet.Core.Worlds;

namespace TerraNet.App.ViewModels;

public sealed record Choice(object Value, string Label, string Sprite, double Unit = 3);

public static class Choices
{
    public static IReadOnlyList<Choice> Sizes { get; } =
    [
        new(WorldSize.Small, Loc.Get("small"), "world", 2),
        new(WorldSize.Medium, Loc.Get("medium"), "world", 3),
        new(WorldSize.Large, Loc.Get("large"), "world", 4)
    ];

    public static IReadOnlyList<Choice> Difficulties { get; } =
        Enum.GetValues<WorldDifficulty>()
            .OrderBy(d => d == WorldDifficulty.Journey ? -1 : (int)d)
            .Select(d => new Choice(d, Loc.Get(d.ToString().ToLowerInvariant()), DifficultySprite(d)))
            .ToList();

    public static IReadOnlyList<Choice> Evils { get; } =
        Enum.GetValues<WorldEvil>()
            .Select(e => new Choice(e, Loc.Get(e.ToString().ToLowerInvariant()), EvilSprite(e)))
            .ToList();

    public static IReadOnlyList<Choice> Themes { get; } =
    [
        new(AppTheme.Auto, Loc.Get("themeAuto"), "globe"),
        new(AppTheme.Light, Loc.Get("themeLight"), "sun"),
        new(AppTheme.Dark, Loc.Get("themeDark"), "moon")
    ];

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
