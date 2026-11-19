namespace TerraNet.Core;

public sealed class AppPaths(string dataRoot)
{
    public string DataRoot { get; } = dataRoot;
    public string Worlds => Ensure(Path.Combine(DataRoot, "worlds"));
    public string ServerWorkspace => Ensure(Path.Combine(DataRoot, "server"));
    public string TerrariaSave => Ensure(Path.Combine(DataRoot, "terraria"));
    public string Downloads => Ensure(Path.Combine(DataRoot, "downloads"));
    public string Plugins => Ensure(Path.Combine(DataRoot, "plugins"));
    public string Settings => Path.Combine(DataRoot, "settings.json");
    public string Identity => Path.Combine(DataRoot, "identity.json");

    private static string Ensure(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
