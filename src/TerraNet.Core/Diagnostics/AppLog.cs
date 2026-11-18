namespace TerraNet.Core.Diagnostics;

public static class AppLog
{
    private const long MaxSize = 1024 * 1024;
    private static readonly Lock Gate = new();

    public static string? Path { get; set; }

    public static void Write(string message)
    {
        if (Path is not { } path)
            return;
        lock (Gate)
        {
            try
            {
                if (File.Exists(path) && new FileInfo(path).Length > MaxSize)
                    File.Move(path, path + ".old", true);
                File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
            catch (IOException)
            {
            }
        }
    }

    public static void Error(string context, Exception error) => Write($"{context}: {error}");
}
