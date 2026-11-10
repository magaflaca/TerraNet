namespace TerraNet.PackTool;

public static class NativeLauncher
{
    private const int ApiLevel = 26;

    public static async Task BuildAsync(string sourceFile, string destination)
    {
        var clang = FindClang();
        await Shell.RunAsync(clang, $"-O2 -fPIE -pie -Wall -Wextra -s -o \"{destination}\" \"{sourceFile}\" -ldl");
    }

    public static string FindClang()
    {
        var roots = new[] { Environment.GetEnvironmentVariable("ANDROID_NDK_ROOT"), Environment.GetEnvironmentVariable("ANDROID_NDK_HOME") }
            .Concat(SdkNdkDirectories())
            .Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d))
            .Cast<string>();

        foreach (var root in roots)
        {
            var bin = Path.Combine(root, "toolchains", "llvm", "prebuilt");
            if (!Directory.Exists(bin))
                continue;
            var host = Directory.GetDirectories(bin).FirstOrDefault();
            if (host is null)
                continue;
            var name = $"aarch64-linux-android{ApiLevel}-clang" + (OperatingSystem.IsWindows() ? ".cmd" : "");
            var path = Path.Combine(host, "bin", name);
            if (File.Exists(path))
                return path;
        }
        throw new FileNotFoundException("Android NDK not found. Set ANDROID_NDK_ROOT.");
    }

    private static IEnumerable<string> SdkNdkDirectories()
    {
        var sdks = new[]
        {
            Environment.GetEnvironmentVariable("ANDROID_HOME"),
            Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT"),
            OperatingSystem.IsWindows() ? @"C:\Android" : null,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk")
        };
        return sdks
            .Where(s => !string.IsNullOrEmpty(s) && Directory.Exists(Path.Combine(s!, "ndk")))
            .SelectMany(s => Directory.GetDirectories(Path.Combine(s!, "ndk")).OrderDescending());
    }
}
