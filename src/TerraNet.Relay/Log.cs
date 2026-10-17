namespace TerraNet.Relay;

public static class Log
{
    private static readonly Lock Gate = new();

    public static void Info(string message) => Write("info", message);

    public static void Warn(string message) => Write("warn", message);

    private static void Write(string level, string message)
    {
        lock (Gate)
            Console.WriteLine($"{DateTime.UtcNow:O} {level} {message}");
    }
}
