using System.Text.Json.Nodes;

namespace TerraNet.PackTool;

public static class ServerStager
{
    private static readonly string[] JunkExtensions = [".pdb", ".xml"];
    private static readonly string[] SatelliteCultures = ["cs", "de", "es", "fr", "it", "ja", "ko", "pl", "pt-BR", "ru", "tr", "zh-Hans", "zh-Hant"];
    private static readonly string[] UnusedPrefixes = ["NuGet."];

    public static void Stage(string tshock, string destination, PackTarget target, string sqlitePath, string runtimeVersion)
    {
        CopyDirectory(tshock, destination);

        var runtimes = Path.Combine(destination, "runtimes");
        PromoteManagedAssets(Path.Combine(runtimes, target.AssetRid, "lib"), Path.Combine(destination, "bin"));
        if (Directory.Exists(runtimes))
            Directory.Delete(runtimes, true);
        var native = Path.Combine(runtimes, target.AssetRid, "native");
        Directory.CreateDirectory(native);
        File.Copy(sqlitePath, Path.Combine(native, target.Sqlite.FileName), true);

        foreach (var junk in Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories)
                     .Where(f => JunkExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                     .ToList())
            File.Delete(junk);

        foreach (var culture in SatelliteCultures.Select(c => Path.Combine(destination, c)).Where(Directory.Exists))
            Directory.Delete(culture, true);
        foreach (var unused in Directory.EnumerateFiles(Path.Combine(destination, "bin"), "*.dll")
                     .Where(f => UnusedPrefixes.Any(p => Path.GetFileName(f).StartsWith(p, StringComparison.Ordinal)))
                     .ToList())
            File.Delete(unused);

        PinRuntime(Path.Combine(destination, "TShock.Server.runtimeconfig.json"), runtimeVersion);
    }

    private static void PromoteManagedAssets(string libraries, string bin)
    {
        if (!Directory.Exists(libraries))
            return;
        foreach (var assembly in Directory.EnumerateFiles(libraries, "*.dll", SearchOption.AllDirectories))
            File.Copy(assembly, Path.Combine(bin, Path.GetFileName(assembly)), true);
    }

    private static void PinRuntime(string runtimeConfig, string runtimeVersion)
    {
        var json = JsonNode.Parse(File.ReadAllText(runtimeConfig))!;
        var options = json["runtimeOptions"]!;
        options["tfm"] = "net" + runtimeVersion[..runtimeVersion.LastIndexOf('.')];
        options["framework"]!["version"] = runtimeVersion[..runtimeVersion.LastIndexOf('.')] + ".0";
        options["configProperties"]!["System.Globalization.Invariant"] = true;
        options["configProperties"]!["System.Globalization.PredefinedCulturesOnly"] = false;
        File.WriteAllText(runtimeConfig, json.ToJsonString(new() { WriteIndented = true }));
    }

    public static void CopyDirectory(string source, string destination)
    {
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)), true);
    }
}
