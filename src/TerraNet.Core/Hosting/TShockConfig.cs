using System.Text.Json;
using System.Text.Json.Nodes;
using TerraNet.Core.Settings;

namespace TerraNet.Core.Hosting;

public static class TShockConfig
{
    private const string FileName = "config.json";
    private const int Unlimited = 1_000_000;

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

    public static void Write(string directory, HostSettings settings)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, FileName);
        var root = Read(path);
        var values = root["Settings"] as JsonObject ?? new JsonObject();
        root["Settings"] = values;

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
        foreach (var (key, relaxed) in Relaxed)
        {
            if (settings.AntiCheat)
                values.Remove(key);
            else
                values[key] = relaxed.DeepClone();
        }

        File.WriteAllText(Path.Combine(directory, "motd.txt"), settings.WelcomeMessage ?? Welcome.Default);
        File.WriteAllText(Path.Combine(directory, "rules.txt"), "");

        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
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
