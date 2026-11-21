using System.Text.Json;

namespace TerraNet.Relay;

public sealed record RelayOptions
{
    public int ListenPort { get; init; } = 9990;
    public int PortRangeStart { get; init; } = 2000;
    public int PortRangeEnd { get; init; } = 2999;
    public string PublicHost { get; init; } = "play.hacks.win";
    public string DataDirectory { get; init; } = "/var/lib/terranet";
    public string CertificatePath { get; init; } = "/etc/terranet/relay.pfx";
    public int MaxSessionsPerAddress { get; init; } = 8;
    public int MaxPlayersPerSession { get; init; } = 64;
    public int PortRetentionDays { get; init; } = 14;
    public int ReclaimIdleHours { get; init; } = 12;

    public static RelayOptions Load(string? path)
    {
        if (path is null || !File.Exists(path))
            return new RelayOptions();
        return JsonSerializer.Deserialize<RelayOptions>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? new RelayOptions();
    }
}
