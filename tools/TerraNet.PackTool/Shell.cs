using System.Diagnostics;

namespace TerraNet.PackTool;

public static class Shell
{
    public static async Task RunAsync(string file, string arguments, string? workingDirectory = null)
    {
        Console.WriteLine($"  $ {file} {arguments}");
        var info = new ProcessStartInfo(file, arguments)
        {
            UseShellExecute = false,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
        };
        using var process = Process.Start(info) ?? throw new InvalidOperationException($"cannot start {file}");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{file} exited with {process.ExitCode}");
    }
}
