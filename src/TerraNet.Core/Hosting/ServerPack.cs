using System.Text.Json;

namespace TerraNet.Core.Hosting;

public sealed record PackManifest(
    string Target,
    string Tshock,
    string Terraria,
    string Runtime,
    string? DotnetRoot,
    string Entry,
    string? StartupHook,
    Dictionary<string, string> Environment);

public sealed class ServerPack
{
    public const string ManifestFile = "pack.json";
    private const string PackToken = "{pack}";

    private ServerPack(string root, PackManifest manifest)
    {
        Root = root;
        Manifest = manifest;
    }

    public string Root { get; }
    public PackManifest Manifest { get; }

    public string? DotnetRoot => Manifest.DotnetRoot is null ? null : Resolve(Manifest.DotnetRoot);
    public string Entry => Resolve(Manifest.Entry);
    public string? StartupHook => Manifest.StartupHook is null ? null : Resolve(Manifest.StartupHook);
    public string ServerDirectory => Path.GetDirectoryName(Entry)!;

    public IReadOnlyDictionary<string, string> Environment =>
        Manifest.Environment.ToDictionary(e => e.Key, e => e.Value.Replace(PackToken, Root));

    public static ServerPack Load(string root)
    {
        var json = File.ReadAllText(Path.Combine(root, ManifestFile));
        var manifest = JsonSerializer.Deserialize<PackManifest>(json, JsonDefaults.Options)
                       ?? throw new InvalidDataException("invalid pack manifest");
        return new ServerPack(root, manifest);
    }

    private string Resolve(string relative) => Path.GetFullPath(Path.Combine(Root, relative));
}
