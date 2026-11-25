using System.Text.Json;

namespace TerraNet.Core.Worlds;

public sealed class WorldLibrary(AppPaths paths)
{
    private const string ProfileExtension = ".world.json";
    private const string WorldExtension = ".wld";

    public event Action? Changed;

    public IReadOnlyList<WorldEntry> List() =>
        Directory.EnumerateFiles(paths.Worlds, "*" + ProfileExtension)
            .Select(ReadProfile)
            .OfType<WorldProfile>()
            .Select(ToEntry)
            .OrderByDescending(w => w.Profile.LastPlayedAt ?? w.Profile.CreatedAt)
            .ToList();

    public WorldEntry Create(WorldProfile profile)
    {
        Write(profile);
        return ToEntry(profile);
    }

    public async Task<WorldEntry?> ImportAsync(Stream source, string fileName)
    {
        var profile = new WorldProfile();
        var target = WorldFile(profile.Id);
        await using (var file = File.Create(target))
            await source.CopyToAsync(file);

        WorldFileInfo? info;
        await using (var file = File.OpenRead(target))
            info = WorldFileReader.TryRead(file);
        if (info is null || !WorldFormat.TryMakeCompatible(target))
        {
            File.Delete(target);
            return null;
        }

        return Create(profile with
        {
            Name = string.IsNullOrWhiteSpace(info.Name) ? Path.GetFileNameWithoutExtension(fileName) : info.Name,
            Size = info.Size,
            Difficulty = info.Difficulty
        });
    }

    public WorldEntry? Find(string id) => List().FirstOrDefault(w => w.Profile.Id == id);

    public bool Rename(string id, string name)
    {
        if (Find(id) is not { } entry || string.IsNullOrWhiteSpace(name))
            return false;
        var trimmed = name.Trim();
        if (entry.Generated && !WorldFileWriter.TryRename(entry.FilePath, trimmed))
            return false;
        Write(entry.Profile with { Name = trimmed });
        return true;
    }

    public async Task<bool> ExportAsync(string id, Stream target)
    {
        if (Find(id) is not { Generated: true } entry)
            return false;
        await using var source = File.OpenRead(entry.FilePath);
        await source.CopyToAsync(target);
        return true;
    }

    public void MarkPlayed(string id)
    {
        if (Find(id) is { } entry)
            Write(entry.Profile with { LastPlayedAt = DateTimeOffset.UtcNow });
    }

    public void Delete(string id)
    {
        foreach (var file in Directory.EnumerateFiles(paths.Worlds, id + ".*"))
            File.Delete(file);
        Changed?.Invoke();
    }

    public string WorldFile(string id) => Path.Combine(paths.Worlds, id + WorldExtension);

    private WorldEntry ToEntry(WorldProfile profile)
    {
        var file = new FileInfo(WorldFile(profile.Id));
        return new WorldEntry(profile, file.FullName, file.Exists, file.Exists ? file.Length : 0);
    }

    private void Write(WorldProfile profile)
    {
        File.WriteAllText(Path.Combine(paths.Worlds, profile.Id + ProfileExtension), JsonSerializer.Serialize(profile, JsonDefaults.Options));
        Changed?.Invoke();
    }

    private static WorldProfile? ReadProfile(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<WorldProfile>(File.ReadAllText(path), JsonDefaults.Options);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }
}

public sealed record WorldEntry(WorldProfile Profile, string FilePath, bool Generated, long SizeBytes);
