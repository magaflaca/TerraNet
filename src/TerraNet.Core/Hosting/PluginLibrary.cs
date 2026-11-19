namespace TerraNet.Core.Hosting;

public sealed record PluginEntry(string Name, string FilePath, bool Enabled, long SizeBytes);

public sealed class PluginLibrary(AppPaths paths)
{
    private const string Extension = ".dll";
    private const string DisabledExtension = ".dll.off";

    public event Action? Changed;

    public IReadOnlyList<PluginEntry> List() =>
        Directory.EnumerateFiles(paths.Plugins)
            .Select(ToEntry)
            .OfType<PluginEntry>()
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public async Task<bool> ImportAsync(Stream source, string fileName)
    {
        if (!fileName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
            return false;
        var name = Path.GetFileNameWithoutExtension(fileName);
        Remove(name);
        await using (var target = File.Create(Path.Combine(paths.Plugins, name + Extension)))
            await source.CopyToAsync(target);
        Changed?.Invoke();
        return true;
    }

    public void SetEnabled(string name, bool enabled)
    {
        var on = Path.Combine(paths.Plugins, name + Extension);
        var off = Path.Combine(paths.Plugins, name + DisabledExtension);
        var (from, to) = enabled ? (off, on) : (on, off);
        if (File.Exists(from))
            File.Move(from, to, true);
        Changed?.Invoke();
    }

    public void Delete(string name)
    {
        Remove(name);
        Changed?.Invoke();
    }

    private void Remove(string name)
    {
        File.Delete(Path.Combine(paths.Plugins, name + Extension));
        File.Delete(Path.Combine(paths.Plugins, name + DisabledExtension));
    }

    private static PluginEntry? ToEntry(string path)
    {
        var file = new FileInfo(path);
        if (file.Name.EndsWith(DisabledExtension, StringComparison.OrdinalIgnoreCase))
            return new PluginEntry(file.Name[..^DisabledExtension.Length], path, false, file.Length);
        if (file.Name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
            return new PluginEntry(file.Name[..^Extension.Length], path, true, file.Length);
        return null;
    }
}
