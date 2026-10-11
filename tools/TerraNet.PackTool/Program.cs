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
RuntimeRoot.Build(runtimePack, target, options.RuntimeVersion, Path.Combine(output, "dotnet"));

var sqlite = await nuget.ResolveAssetAsync(target.Sqlite, options.SqliteVersion);
var server = Path.Combine(output, "server");
ServerStager.Stage(tshock, server, target, sqlite, options.RuntimeVersion);

foreach (var plugin in options.Plugins)
    File.Copy(plugin, Path.Combine(server, "ServerPlugins", Path.GetFileName(plugin)), true);

if (target.Host == HostKind.AppHost)
{
    var hostPack = await nuget.ExtractAsync($"microsoft.netcore.app.host.{target.RuntimeRid}", options.RuntimeVersion);
    var exe = target.RuntimeRid.StartsWith("win", StringComparison.Ordinal) ? ".exe" : "";
    var template = Path.Combine(hostPack, "runtimes", target.RuntimeRid, "native", "apphost" + exe);
    AppHostWriter.Write(template, Path.Combine(server, "TShock.Server" + exe), "TShock.Server.dll");
}
else
{
    await NativeLauncher.BuildAsync(Path.Combine(repo, "src", "native", "tnhost", "tnhost.c"), Path.Combine(output, "libtnhost.so"));
}

var manifest = new
{
    target = target.Name,
    tshock = options.TShockTag.TrimStart('v'),
    runtime = options.RuntimeVersion,
    entry = "server/TShock.Server.dll"
};
File.WriteAllText(Path.Combine(output, "pack.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("done");

internal sealed record PackOptions(string Target, string? Output, string TShockTag, string RuntimeVersion, string SqliteVersion, IReadOnlyList<string> Plugins)
{
    public static PackOptions Parse(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: packtool <target> [--out dir] [--tshock tag] [--runtime version] [--plugin file.dll]");

        string? Value(string name) => args.SkipWhile(a => a != name).Skip(1).FirstOrDefault();
        var plugins = args.Select((a, i) => (a, i)).Where(x => x.a == "--plugin").Select(x => args[x.i + 1]).ToList();

        return new PackOptions(
            args[0],
            Value("--out"),
            Value("--tshock") ?? "v6.2.1",
            Value("--runtime") ?? "10.0.15",
            Value("--sqlite") ?? "2.1.10",
            plugins);
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
