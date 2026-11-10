using System.IO.Compression;
using System.Text.Json;
using TerraNet.PackTool;

var options = PackOptions.Parse(args);
var target = PackTarget.Find(options.Target);
var repo = RepoRoot.Find();
var cache = Path.Combine(repo, ".cache");
var artifacts = options.Output ?? Path.Combine(repo, "artifacts", "server");
var output = Path.Combine(artifacts, target.Name);

Console.WriteLine($"pack {target.Name} -> {output}");
if (Directory.Exists(output))
    Directory.Delete(output, true);
Directory.CreateDirectory(output);

var nuget = new NugetStore(cache);
var tshock = await new TShockSource(cache).PublishAsync(options.TShockTag);
var sqlite = await nuget.ResolveAssetAsync(target.Sqlite, options.SqliteVersion);
var server = Path.Combine(output, "server");
ServerStager.Stage(tshock, server, target, sqlite, options.RuntimeVersion);

var agent = await AgentBuilder.BuildAsync(repo, tshock, cache);
var agentPath = Path.Combine(server, "ServerPlugins", Path.GetFileName(agent));
File.Copy(agent, agentPath, true);

string? dotnetRoot = null;
var environment = new Dictionary<string, string>();
if (target.Host == HostKind.NativeLauncher)
{
    var runtimePack = await nuget.ExtractAsync($"microsoft.netcore.app.runtime.{target.RuntimeRid}", options.RuntimeVersion);
    var runtimeDirectory = RuntimeRoot.Build(runtimePack, target, options.RuntimeVersion, Path.Combine(output, "dotnet"));
    dotnetRoot = "dotnet";

    var openssl = await new TermuxPackage(cache).ExtractAsync("openssl", target.OpenSslFiles);
    foreach (var file in target.OpenSslFiles)
        File.Copy(Path.Combine(openssl, Path.GetFileName(file)), Path.Combine(runtimeDirectory, Path.GetFileName(file)), true);

    var bin = Path.Combine(server, "bin");
    var plugins = Path.Combine(server, "ServerPlugins");
    string[] hot =
    [
        Path.Combine(bin, "OTAPI.dll"),
        Path.Combine(bin, "TerrariaServer.dll"),
        Path.Combine(plugins, "TShockAPI.dll"),
        Path.Combine(runtimeDirectory, "System.Private.CoreLib.dll"),
        Path.Combine(runtimeDirectory, "System.Collections.dll"),
        Path.Combine(runtimeDirectory, "System.Linq.dll")
    ];
    await new AotCompiler(nuget, cache, options.RuntimeVersion).CompileAsync(hot, [runtimeDirectory, bin, server, plugins]);
    await NativeLauncher.BuildAsync(Path.Combine(repo, "src", "native", "tnhost", "tnhost.c"), Path.Combine(output, "libtnhost.so"));

    var relativeRuntime = Path.GetRelativePath(output, runtimeDirectory).Replace('\\', '/');
    foreach (var (key, value) in target.Environment)
        environment[key] = value.Replace(PackTarget.RuntimeDirectoryToken, PackTarget.PackToken + "/" + relativeRuntime);
}
else
{
    File.Copy(sqlite, Path.Combine(server, "bin", target.Sqlite.FileName), true);
    foreach (var (key, value) in target.Environment)
        environment[key] = value;
}

var manifest = new PackManifest(
    target.Name,
    options.TShockTag.TrimStart('v'),
    options.TerrariaVersion,
    options.RuntimeVersion,
    dotnetRoot,
    "server/TShock.Server.dll",
    target.Host == HostKind.NativeLauncher ? Path.GetRelativePath(output, agentPath).Replace('\\', '/') : null,
    environment);
File.WriteAllText(Path.Combine(output, "pack.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

if (target.Host == HostKind.SelfHosted)
{
    var zip = Path.Combine(artifacts, target.Name + ".zip");
    File.Delete(zip);
    ZipFile.CreateFromDirectory(output, zip, CompressionLevel.SmallestSize, false);
    Console.WriteLine($"  zip {new FileInfo(zip).Length / 1048576.0:0.0} MB");
}
Console.WriteLine("done");

internal sealed record PackManifest(
    string Target,
    string Tshock,
    string Terraria,
    string Runtime,
    string? DotnetRoot,
    string Entry,
    string? StartupHook,
    IReadOnlyDictionary<string, string> Environment);

internal sealed record PackOptions(string Target, string? Output, string TShockTag, string TerrariaVersion, string RuntimeVersion, string SqliteVersion)
{
    public static PackOptions Parse(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: packtool <target> [--out dir] [--tshock tag] [--terraria version] [--runtime version]");

        string? Value(string name) => args.SkipWhile(a => a != name).Skip(1).FirstOrDefault();

        return new PackOptions(
            args[0],
            Value("--out"),
            Value("--tshock") ?? "v6.2.1",
            Value("--terraria") ?? "1.4.5.8",
            Value("--runtime") ?? "10.0.15",
            Value("--sqlite") ?? "2.1.10");
    }
}

internal static class RepoRoot
{
    public static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
            dir = dir.Parent;
        return dir?.FullName ?? Environment.CurrentDirectory;
    }
}
