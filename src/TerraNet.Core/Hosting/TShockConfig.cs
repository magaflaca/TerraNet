using System.Text.Json;
using System.Text.Json.Nodes;
using TerraNet.Core.Settings;

namespace TerraNet.Core.Hosting;

public static class TShockConfig
{
    private const string FileName = "config.json";
    private const int Unlimited = 1_000_000;

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
        values["SpawnProtection"] = false;
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
        values["RangeChecks"] = false;
        values["KickOnDamageThresholdBroken"] = false;
        values["MaxDamage"] = Unlimited;
        values["MaxProjDamage"] = Unlimited;
        values["TileKillThreshold"] = Unlimited;
        values["TilePlaceThreshold"] = Unlimited;
        values["TileLiquidThreshold"] = Unlimited;
        values["TilePaintThreshold"] = Unlimited;
        values["ProjectileThreshold"] = Unlimited;
        values["HealOtherThreshold"] = Unlimited;
        values["KickOnTileKillThresholdBroken"] = false;
        values["KickOnTilePlaceThresholdBroken"] = false;
        values["KickOnTileLiquidThresholdBroken"] = false;
        values["KickOnTilePaintThresholdBroken"] = false;
        values["KickOnProjectileThresholdBroken"] = false;
        values["KickOnHealOtherThresholdBroken"] = false;
        values["AnonymousBossInvasions"] = true;

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
