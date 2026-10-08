using System.IO.Compression;

namespace TerraNet.PackTool;

public sealed class NugetStore(string cacheRoot)
{
    private static readonly HttpClient Http = new();

    public async Task<string> ExtractAsync(string package, string version)
    {
        var id = package.ToLowerInvariant();
        var target = Path.Combine(cacheRoot, "nuget", $"{id}.{version}");
        if (Directory.Exists(target))
            return target;

        var url = $"https://api.nuget.org/v3-flatcontainer/{id}/{version}/{id}.{version}.nupkg";
        Console.WriteLine($"  fetch {id} {version}");
        await using var stream = await Http.GetStreamAsync(url);
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        memory.Position = 0;

        var staging = target + ".tmp";
        if (Directory.Exists(staging))
            Directory.Delete(staging, true);
        ZipFile.ExtractToDirectory(memory, staging);
        Directory.Move(staging, target);
        return target;
    }

    public async Task<string> ResolveAssetAsync(NativeAsset asset, string version)
    {
        var root = await ExtractAsync(asset.Package, version);
        var entry = Path.Combine(root, asset.EntryPath);
        if (asset.NestedEntryPath is null)
            return entry;

        var nested = Path.Combine(root, "nested", asset.NestedEntryPath);
        if (File.Exists(nested))
            return nested;

        using var archive = ZipFile.OpenRead(entry);
        var item = archive.GetEntry(asset.NestedEntryPath)
                   ?? throw new FileNotFoundException(asset.NestedEntryPath);
        Directory.CreateDirectory(Path.GetDirectoryName(nested)!);
        item.ExtractToFile(nested, true);
        return nested;
    }
}
