using System.Diagnostics;

namespace TerraNet.PackTool;

public static class Shell
{
    public static async Task RunAsync(string file, string arguments, string? workingDirectory = null, IReadOnlyDictionary<string, string>? environment = null, bool quiet = false)
    {
        if (!quiet)
            Console.WriteLine($"  $ {file} {arguments}");
        var info = new ProcessStartInfo(file, arguments)
        {
            UseShellExecute = false,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardOutput = quiet,
            RedirectStandardError = quiet
        };
        foreach (var (key, value) in environment ?? new Dictionary<string, string>())
            info.Environment[key] = value;

        using var process = Process.Start(info) ?? throw new InvalidOperationException($"cannot start {file}");
        var output = quiet ? process.StandardOutput.ReadToEndAsync() : Task.FromResult("");
        var errors = quiet ? process.StandardError.ReadToEndAsync() : Task.FromResult("");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{file} exited with {process.ExitCode}\n{await output}\n{await errors}");
    }
}
