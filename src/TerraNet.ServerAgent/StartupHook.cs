using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using TerraNet.Agent;

internal static class StartupHook
{
    private static bool IsMono => Type.GetType("Mono.RuntimeStructs") is not null;

    public static void Initialize()
    {
        AssemblyLoadContext.Default.Resolving += ResolveFromBin;
        InstallConsoleBridge();
        if (IsMono)
            PrimeTerraria();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InstallConsoleBridge() => TerraNet.ServerAgent.ConsoleBridge.SuppressVanillaInput();

    private static System.Reflection.Assembly? ResolveFromBin(AssemblyLoadContext context, System.Reflection.AssemblyName name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "bin", name.Name + ".dll");
        return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void PrimeTerraria()
    {
        var saveDirectory = Environment.GetEnvironmentVariable(AgentWire.SaveDirectoryVariable);
        Terraria.Program.LaunchParameters = new Dictionary<string, string>();
        Terraria.Program.SavePath = string.IsNullOrEmpty(saveDirectory)
            ? ReLogic.OS.Platform.Get<ReLogic.OS.IPathService>().GetStoragePath("Terraria")
            : saveDirectory;
        RuntimeHelpers.RunClassConstructor(typeof(Terraria.Main).TypeHandle);
    }
}
