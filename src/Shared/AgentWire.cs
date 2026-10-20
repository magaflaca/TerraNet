using System.Text.Json;
using System.Text.Json.Serialization;

namespace TerraNet.Agent;

public static class AgentWire
{
    public const string Prefix = "@terranet ";
    public const string SaveDirectoryVariable = "TERRANET_SAVE_DIR";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Encode(AgentEvent value) => Prefix + JsonSerializer.Serialize(value, Options);

    public static AgentEvent? TryDecode(string line)
    {
        if (!line.StartsWith(Prefix, StringComparison.Ordinal))
            return null;
        try
        {
            return JsonSerializer.Deserialize<AgentEvent>(line.AsSpan(Prefix.Length), Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public static class AgentEventKind
{
    public const string Ready = "ready";
    public const string Players = "players";
    public const string Saved = "saved";
}

public sealed record AgentEvent(string Kind, string? World = null, int? Port = null, int? MaxPlayers = null, string[]? Players = null);
