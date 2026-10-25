using System.Globalization;

namespace TerraNet.Core.Localization;

public static class Loc
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Tables =
        new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = Strings.English,
            ["es"] = Strings.Spanish
        };

    public static string Language { get; private set; } = Detect(CultureInfo.CurrentUICulture);

    public static void Use(CultureInfo culture) => Language = Detect(culture);

    public static string Get(string key) =>
        Tables[Language].TryGetValue(key, out var value) || Tables["en"].TryGetValue(key, out value) ? value : key;

    public static string Format(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, Get(key), args);

    public static string Pick(IReadOnlyDictionary<string, string> localized) =>
        localized.TryGetValue(Language, out var value) || localized.TryGetValue("en", out value) ? value : localized.Values.FirstOrDefault() ?? "";

    private static string Detect(CultureInfo culture) =>
        Tables.ContainsKey(culture.TwoLetterISOLanguageName) ? culture.TwoLetterISOLanguageName : "en";
}
