using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TerraNet.Core.Localization;

namespace TerraNet.Core.Hosting;

public enum TShockOptionType
{
    Bool,
    Int,
    Text,
    Choice
}

public sealed record TShockOption(
    string Section,
    string? File,
    string Key,
    TShockOptionType Type,
    IReadOnlyList<string>? Options,
    JsonNode? Default,
    IReadOnlyList<string> En,
    IReadOnlyList<string> Es)
{
    public const string SscFile = "ssc";

    public bool IsSsc => File == SscFile;
    public string Id => (IsSsc ? SscFile : "config") + ":" + Key;
    public string Label => Text(0);
    public string Hint => Text(1);

    private string Text(int index) => (Loc.Language == "es" ? Es : En).ElementAtOrDefault(index) ?? "";
}

public static class TShockOptions
{
    private const string Resource = "TerraNet.Core.Hosting.tshock-options.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static IReadOnlyList<TShockOption> All { get; } = Load();

    private static IReadOnlyList<TShockOption> Load()
    {
        using var stream = typeof(TShockOptions).Assembly.GetManifestResourceStream(Resource)
                           ?? throw new InvalidOperationException("tshock options missing");
        return JsonSerializer.Deserialize<List<TShockOption>>(stream, Options) ?? [];
    }
}
