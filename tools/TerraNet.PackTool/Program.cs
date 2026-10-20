using System.Text.Json;
using TerraNet.PackTool;

var options = PackOptions.Parse(args);
var target = PackTarget.Find(options.Target);
var repo = RepoRoot.Find();
var cache = Path.Combine(repo, ".cache");
var output = Path.Combine(options.Output ?? Path.Combine(repo, "artifacts", "server"), target.Name);

Console.WriteLine($"pack {target.Name} -> {output}");
if (Directory.Exists(output))
    Directory.Delete(output, true);
Directory.CreateDirectory(output);

var nuget = new NugetStore(cache);
var tshock = await new TShockSource(cache).PublishAsync(options.TShockTag);

var runtimePack = await nuget.ExtractAsync($"microsoft.netcore.app.runtime.{target.RuntimeRid}", options.RuntimeVersion);
var dotnetRoot = Path.Combine(output, "dotnet");
var runtimeDirectory = RuntimeRoot.Build(runtimePack, target, options.RuntimeVersion, dotnetRoot);

if (target.OpenSslFiles.Count > 0)
{
    var openssl = await new TermuxPackage(cache).ExtractAsync("openssl", target.OpenSslFiles);
    foreach (var file in target.OpenSslFiles)
        File.Copy(Path.Combine(openssl, Path.GetFileName(file)), Path.Combine(runtimeDirectory, Path.GetFileName(file)), true);
}

var sqlite = await nuget.ResolveAssetAsync(target.Sqlite, options.SqliteVersion);
var server = Path.Combine(output, "server");
ServerStager.Stage(tshock, server, target, sqlite, options.RuntimeVersion);

var agent = await AgentBuilder.BuildAsync(repo, tshock, cache);
File.Copy(agent, Path.Combine(server, "ServerPlugins", Path.GetFileName(agent)), true);

string? host = null;
if (target.Host == HostKind.AppHost)
{
    var hostPack = await nuget.ExtractAsync($"microsoft.netcore.app.host.{target.RuntimeRid}", options.RuntimeVersion);
    var template = Path.Combine(hostPack, "runtimes", target.RuntimeRid, "native", "apphost" + target.ExecutableSuffix);
    host = "server/TShock.Server" + target.ExecutableSuffix;
    AppHostWriter.Write(template, Path.Combine(output, host), "TShock.Server.dll");
}
else
{
    await NativeLauncher.BuildAsync(Path.Combine(repo, "src", "native", "tnhost", "tnhost.c"), Path.Combine(output, "libtnhost.so"));
}

var relativeRuntime = Path.GetRelativePath(output, runtimeDirectory).Replace('\\', '/');
var manifest = new PackManifest(
    target.Name,
    options.TShockTag.TrimStart('v'),
    options.TerrariaVersion,
    options.RuntimeVersion,
    "dotnet",
    "server/TShock.Server.dll",
    host,
    "server/ServerPlugins/" + Path.GetFileName(agent),
    target.Environment.ToDictionary(e => e.Key, e => e.Value.Replace(PackTarget.RuntimeDirectoryToken, relativeRuntime)));

File.WriteAllText(Path.Combine(output, "pack.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
Console.WriteLine("done");

internal sealed record PackManifest(
    string Target,
    string Tshock,
    string Terraria,
    string Runtime,
    string DotnetRoot,
    string Entry,
    string? Host,
    string StartupHook,
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
