using System.Text.Json;

namespace TerraNet.Core.Storage;

public sealed class JsonStore<T>(string path) where T : class, new()
{
    private readonly Lock _gate = new();

    public T Load()
    {
        lock (_gate)
        {
            if (!File.Exists(path))
                return new T();
            try
            {
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonDefaults.Options) ?? new T();
            }
            catch (JsonException)
            {
                return new T();
            }
        }
    }

    public void Save(T value)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(value, JsonDefaults.Options));
            File.Move(temp, path, true);
        }
    }
}
