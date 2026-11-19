namespace TerraNet.Core.Localization;

public static class WorldNames
{
    public const int MaxLength = 27;

    public static string Random()
    {
        var (first, second) = Strings.WorldNames.TryGetValue(Loc.Language, out var pair) ? pair : Strings.WorldNames["en"];
        return $"{first[System.Random.Shared.Next(first.Length)]} {second[System.Random.Shared.Next(second.Length)]}";
    }
}
