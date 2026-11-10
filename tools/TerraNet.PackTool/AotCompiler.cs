using System.Runtime.InteropServices;

namespace TerraNet.PackTool;

public sealed class AotCompiler(NugetStore nuget, string cache, string runtimeVersion)
{
    private const string Triple = "aarch64-linux-android";

    public async Task CompileAsync(IReadOnlyList<string> assemblies, IReadOnlyList<string> searchDirectories)
    {
        var host = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-x64" : "linux-x64";
        var cross = await nuget.ExtractAsync($"microsoft.netcore.app.runtime.aot.{host}.cross.android-arm64", runtimeVersion);
        var tools = Path.Combine(cross, "tools");
        var compiler = Path.Combine(tools, "mono-aot-cross" + (host.StartsWith("win") ? ".exe" : ""));
        var clang = NativeLauncher.FindClang();

        var stubs = Path.Combine(cache, "aot-stubs");
        if (Directory.Exists(stubs))
            Directory.Delete(stubs, true);
        foreach (var (replacement, original) in StubAssemblies.Generate(assemblies, searchDirectories, stubs))
            File.Copy(replacement, original, true);
        var monoPath = string.Join(Path.PathSeparator, searchDirectories.Prepend(stubs));
        var work = Path.Combine(cache, "aot-work");
        Directory.CreateDirectory(work);

        await Parallel.ForEachAsync(assemblies, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) }, async (assembly, _) =>
        {
            var name = Path.GetFileNameWithoutExtension(assembly);
            var asm = Path.Combine(work, name + ".s");
            var obj = Path.Combine(work, name + "-llvm.o");
            var options = $"asmonly,llvm-path={tools},outfile={asm},llvm-outfile={obj},mtriple={Triple}";
            await Shell.RunAsync(compiler, $"--llvm --aot={options} \"{assembly}\"", environment: new Dictionary<string, string> { ["MONO_PATH"] = monoPath }, quiet: true);
            await Shell.RunAsync(clang, $"-shared -Wl,--no-relax -s -o \"{assembly}.so\" \"{asm}\" \"{obj}\"", quiet: true);
            Console.WriteLine($"  aot {Path.GetFileName(assembly)} ({new FileInfo(assembly + ".so").Length / 1048576.0:0.0} MB)");
        });
    }
}
