using System.Text.Json;
using System.Text.Json.Nodes;
using TerraNet.Core.Settings;

namespace TerraNet.Core.Hosting;

public static class TShockConfig
{
    private const string FileName = "config.json";
    private const string SscFileName = "sscconfig.json";
    private const int Unlimited = 1_000_000;
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    private static readonly (string Key, JsonNode Value)[] Relaxed =
    [
        ("RangeChecks", false),
        ("KickOnDamageThresholdBroken", false),
        ("MaxDamage", Unlimited),
        ("MaxProjDamage", Unlimited),
        ("TileKillThreshold", Unlimited),
        ("TilePlaceThreshold", Unlimited),
        ("TileLiquidThreshold", Unlimited),
        ("TilePaintThreshold", Unlimited),
        ("ProjectileThreshold", Unlimited),
        ("HealOtherThreshold", Unlimited),
        ("KickOnTileKillThresholdBroken", false),
        ("KickOnTilePlaceThresholdBroken", false),
        ("KickOnTileLiquidThresholdBroken", false),
        ("KickOnTilePaintThresholdBroken", false),
        ("KickOnProjectileThresholdBroken", false),
        ("KickOnHealOtherThresholdBroken", false)
    ];

    public static JsonObject Managed(HostSettings settings)
    {
        var values = new JsonObject();
        values["ServerPassword"] = settings.Password;
        values["MaxSlots"] = settings.MaxPlayers;
        values["ReservedSlots"] = 0;
        values["EnableGeoIP"] = false;
        values["KickProxyUsers"] = false;
        values["RestApiEnabled"] = false;
        values["SpawnProtection"] = settings.SpawnProtection;
        values["SpawnProtectionRadius"] = Math.Clamp(settings.SpawnProtectionRadius, 1, 100);
        values["PvPMode"] = settings.Pvp.ToString().ToLowerInvariant();
        values["AutoSave"] = true;
        values["AnnounceSave"] = false;
        values["SaveWorldOnCrash"] = true;
        values["SaveWorldOnLastPlayerExit"] = true;
        values["ShowBackupAutosaveMessages"] = false;
        values["BackupInterval"] = Math.Max(5, settings.AutoSaveMinutes);
        values["BackupKeepFor"] = Math.Max(30, settings.AutoSaveMinutes * 6);
        values["DisableLoginBeforeJoin"] = true;
        values["PreventDeadModification"] = false;
        values["PreventInvalidPlaceStyle"] = false;
        values["DisableCustomDeathMessages"] = true;
        values["SuppressPermissionFailureNotices"] = true;
        values["DisplayIPToAdmins"] = false;
        values["DisableUUIDLogin"] = true;
        values["EnableChatAboveHeads"] = true;
        values["AnonymousBossInvasions"] = true;
        if (!settings.AntiCheat)
            foreach (var (key, relaxed) in Relaxed)
                values[key] = relaxed.DeepClone();
        return values;
    }

    public static JsonNode? Effective(HostSettings settings, TShockOption option)
    {
        if (settings.TShockOverrides.TryGetValue(option.Id, out var raw))
            return JsonNode.Parse(raw);
        if (!option.IsSsc && Managed(settings)[option.Key] is { } managed)
            return managed.DeepClone();
        return option.Default?.DeepClone();
    }

    public static void Write(string directory, HostSettings settings)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, FileName);
        var root = Read(path);
        var values = root["Settings"] as JsonObject ?? new JsonObject();
        root["Settings"] = values;

        if (settings.AntiCheat)
            foreach (var (key, _) in Relaxed)
                values.Remove(key);
        foreach (var (key, value) in Managed(settings))
            values[key] = value?.DeepClone();

        var sscPath = Path.Combine(directory, SscFileName);
        var sscRoot = Read(sscPath);
        var ssc = sscRoot["Settings"] as JsonObject ?? new JsonObject();
        sscRoot["Settings"] = ssc;

        foreach (var option in TShockOptions.All)
            if (settings.TShockOverrides.TryGetValue(option.Id, out var raw))
                (option.IsSsc ? ssc : values)[option.Key] = JsonNode.Parse(raw);

        File.WriteAllText(Path.Combine(directory, "motd.txt"), settings.WelcomeMessage ?? Welcome.Default);
        File.WriteAllText(Path.Combine(directory, "rules.txt"), "");
        File.WriteAllText(path, root.ToJsonString(Indented));
        File.WriteAllText(sscPath, sscRoot.ToJsonString(Indented));
    }

    private static JsonObject Read(string path)
    {
        if (!File.Exists(path))
            return new JsonObject();
        try
        {
            return JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }
}
