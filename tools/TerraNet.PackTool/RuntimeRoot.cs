using System.Text.Json;
using System.Text.Json.Nodes;

namespace TerraNet.PackTool;

public static class RuntimeRoot
{
    private const string Framework = "Microsoft.NETCore.App";
    private static readonly string[] SkippedExtensions = [".a", ".lib", ".h", ".pdb", ".dbg", ".xml", ".json"];

    public static string Build(string runtimePack, PackTarget target, string version, string destination)
    {
        var source = Path.Combine(runtimePack, "runtimes", target.RuntimeRid);
        var managed = Directory.GetDirectories(Path.Combine(source, "lib")).Single();
        var native = Path.Combine(source, "native");

        var shared = Path.Combine(destination, "shared", Framework, version);
        var fxr = Path.Combine(destination, "host", "fxr", version);
        Directory.CreateDirectory(shared);
        Directory.CreateDirectory(fxr);

        foreach (var file in Directory.GetFiles(managed, "*.dll"))
            File.Copy(file, Path.Combine(shared, Path.GetFileName(file)), true);

        foreach (var file in Directory.GetFiles(native))
        {
            var name = Path.GetFileName(file);
            if (SkippedExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase))
                continue;
            var isFxr = name.StartsWith("libhostfxr", StringComparison.Ordinal) || name.StartsWith("hostfxr", StringComparison.Ordinal);
            File.Copy(file, Path.Combine(isFxr ? fxr : shared, name), true);
        }

        WriteFrameworkDeps(shared, target, version);
        return shared;
    }

    private static void WriteFrameworkDeps(string shared, PackTarget target, string version)
    {
        var tfm = $".NETCoreApp,Version=v{version[..version.LastIndexOf('.')]}/{target.RuntimeRid}";
        var library = $"runtimepack.{Framework}.Runtime.{target.RuntimeRid}/{version}";
        var files = Directory.GetFiles(shared).Select(Path.GetFileName).OfType<string>().ToList();

        JsonObject Entries(Func<string, bool> filter) =>
            new(files.Where(filter).Select(f => KeyValuePair.Create(f, (JsonNode?)new JsonObject())));

        var deps = new JsonObject
        {
            ["runtimeTarget"] = new JsonObject { ["name"] = tfm, ["signature"] = "" },
            ["compilationOptions"] = new JsonObject(),
            ["targets"] = new JsonObject
            {
                [tfm] = new JsonObject
                {
                    [library] = new JsonObject
                    {
                        ["runtime"] = Entries(f => f.EndsWith(".dll", StringComparison.Ordinal) && IsManaged(Path.Combine(shared, f))),
                        ["native"] = Entries(f => !f.EndsWith(".dll", StringComparison.Ordinal) || !IsManaged(Path.Combine(shared, f)))
                    }
                }
            },
            ["libraries"] = new JsonObject
            {
                [library] = new JsonObject { ["type"] = "runtimepack", ["serviceable"] = false, ["sha512"] = "" }
            }
        };

        File.WriteAllText(Path.Combine(shared, $"{Framework}.deps.json"), deps.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static bool IsManaged(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new System.Reflection.PortableExecutable.PEReader(stream);
        try
        {
            return reader.HasMetadata;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }
}
