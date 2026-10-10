using System.Text.Json.Nodes;

namespace TerraNet.PackTool;

public static class ServerStager
{
    private static readonly string[] JunkExtensions = [".pdb", ".xml"];
    private static readonly string[] ForeignPrefixes = ["System.", "Microsoft.", "Mono.Cecil", "NuGet.", "MySql.", "Npgsql", "Google.", "BouncyCastle", "K4os", "ZstdSharp"];

    public static void Stage(string tshock, string destination, PackTarget target, string sqlitePath, string runtimeVersion)
    {
        CopyDirectory(tshock, destination);

        var runtimes = Path.Combine(destination, "runtimes");
        if (Directory.Exists(runtimes))
            Directory.Delete(runtimes, true);
        var native = Path.Combine(runtimes, target.AssetRid, "native");
        Directory.CreateDirectory(native);
        File.Copy(sqlitePath, Path.Combine(native, target.Sqlite.FileName), true);

        foreach (var junk in Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories)
                     .Where(f => JunkExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                     .ToList())
            File.Delete(junk);

        PinRuntime(Path.Combine(destination, "TShock.Server.runtimeconfig.json"), runtimeVersion);
        PatchAssemblies(destination);
    }

    private static void PatchAssemblies(string root)
    {
        var bin = Path.Combine(root, "bin");
        foreach (var assembly in Directory.EnumerateFiles(root, "*.dll", SearchOption.AllDirectories)
                     .Where(f => !ForeignPrefixes.Any(p => Path.GetFileName(f).StartsWith(p, StringComparison.Ordinal)))
                     .Where(f => !f.Contains(Path.DirectorySeparatorChar + "runtimes" + Path.DirectorySeparatorChar)))
        {
            try
            {
                var count = PlatformPatcher.Patch(assembly, bin);
                if (count > 0)
                    Console.WriteLine($"  patched {Path.GetRelativePath(root, assembly)} ({count})");
            }
            catch (BadImageFormatException)
            {
            }
        }
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
