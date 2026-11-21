using System.IO.Compression;
using System.Text;
using TerraNet.Core.Localization;
using TerraNet.Core.Settings;
using TerraNet.Core.Worlds;

namespace TerraNet.Core.Hosting;

public static class ServerExport
{
    private const string ConfigFolder = "tshock";
    private const string PluginFolder = "ServerPlugins";
    private const string WorldFolder = "worlds";
    private static readonly string[] SkippedFolders = ["backups", "logs"];
    private static readonly string[] Regenerated = ["config.json", "sscconfig.json", "motd.txt", "rules.txt"];
    private static readonly string[] Skipped = ["tshock.pid"];

    public static string SuggestedName() => $"terranet-tshock-{DateTime.Now:yyyy-MM-dd}.zip";

    public static async Task WriteAsync(Stream target, AppPaths paths, HostSettings settings, WorldLibrary worlds, PluginLibrary plugins, bool includeWorlds)
    {
        using var zip = new ZipArchive(target, ZipArchiveMode.Create, leaveOpen: true);
        var workspace = Path.Combine(paths.ServerWorkspace, ConfigFolder);

        if (Directory.Exists(workspace))
            foreach (var file in Directory.EnumerateFiles(workspace, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(workspace, file);
                var top = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
                if (SkippedFolders.Contains(top, StringComparer.OrdinalIgnoreCase) || Regenerated.Contains(relative, StringComparer.OrdinalIgnoreCase) || Skipped.Contains(relative, StringComparer.OrdinalIgnoreCase))
                    continue;
                await AddFileAsync(zip, file, $"{ConfigFolder}/{relative.Replace('\\', '/')}");
            }

        var staging = Path.Combine(paths.Downloads, "export-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(staging);
            foreach (var name in Regenerated.Take(2))
                if (File.Exists(Path.Combine(workspace, name)))
                    File.Copy(Path.Combine(workspace, name), Path.Combine(staging, name));
            TShockConfig.Write(staging, settings);
            foreach (var name in Regenerated)
                await AddFileAsync(zip, Path.Combine(staging, name), $"{ConfigFolder}/{name}");
        }
        finally
        {
            try
            {
                Directory.Delete(staging, true);
            }
            catch (IOException)
            {
            }
        }

        foreach (var plugin in plugins.List().Where(p => p.Enabled))
            await AddFileAsync(zip, plugin.FilePath, $"{PluginFolder}/{plugin.Name}.dll");

        string? startWorld = null;
        if (includeWorlds)
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ordered = worlds.List().Where(w => w.Generated && File.Exists(w.FilePath))
                .OrderByDescending(w => w.Profile.Id == settings.LastWorldId);
            foreach (var world in ordered)
            {
                var name = UniqueName(SafeName(world.Profile.Name), used);
                var entry = $"{WorldFolder}/{name}.wld";
                await AddFileAsync(zip, world.FilePath, entry);
                startWorld ??= entry;
            }
        }

        var worldArgument = startWorld is null ? "" : $" -world \"{startWorld}\"";
        var arguments = $"-port 7777 -maxplayers {settings.MaxPlayers}{worldArgument}";
        AddText(zip, "start.bat", $"@echo off\r\nchcp 65001 >nul\r\ncd /d \"%~dp0\"\r\nTShock.Server.exe {arguments.Replace('/', '\\')}\r\npause\r\n");
        AddText(zip, "start.sh", $"#!/bin/sh\ncd \"$(dirname \"$0\")\"\n./TShock.Server {arguments}\n", unixExecutable: true);
        AddText(zip, Loc.Get("exportReadmeFile"), Loc.Get("exportReadme").Replace("\n", Environment.NewLine));
    }

    private static async Task AddFileAsync(ZipArchive zip, string path, string entryName)
    {
        await using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        entry.LastWriteTime = File.GetLastWriteTime(path);
        await using var target = entry.Open();
        await source.CopyToAsync(target);
    }

    private static void AddText(ZipArchive zip, string entryName, string text, bool unixExecutable = false)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        if (unixExecutable)
            entry.ExternalAttributes = Convert.ToInt32("100755", 8) << 16;
        using var stream = entry.Open();
        var bytes = new UTF8Encoding(false).GetBytes(text);
        stream.Write(bytes);
    }

    private static string SafeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|']).ToHashSet();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
        return cleaned.Length == 0 ? "world" : cleaned;
    }

    private static string UniqueName(string name, HashSet<string> used)
    {
        var candidate = name;
        for (var index = 2; !used.Add(candidate); index++)
            candidate = $"{name} ({index})";
        return candidate;
    }
}
