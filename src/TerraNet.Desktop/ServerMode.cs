using System.Reflection;
using System.Runtime.Loader;

namespace TerraNet.Desktop;

public static class ServerMode
{
    public const string Flag = "--server";
    private const string Agent = "ServerPlugins/TerraNet.ServerAgent.dll";
    private const string Entry = "TShock.Server.dll";

    public static int Run(string[] args)
    {
        HiddenConsole.Attach();
        var serverDirectory = Path.GetFullPath(args[1]) + Path.DirectorySeparatorChar;
        AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", serverDirectory);

        var probe = new[] { serverDirectory, Path.Combine(serverDirectory, "bin") };
        AssemblyLoadContext.Default.Resolving += (context, name) =>
            probe.Select(d => Path.Combine(d, name.Name + ".dll")).FirstOrDefault(File.Exists) is { } path
                ? context.LoadFromAssemblyPath(path)
                : null;

        var agent = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(serverDirectory, Agent));
        agent.GetType("StartupHook", true)!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);

        var server = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(serverDirectory, Entry));
        var result = server.EntryPoint!.Invoke(null, [args[2..]]);
        return result switch
        {
            int code => code,
            Task<int> task => task.GetAwaiter().GetResult(),
            _ => 0
        };
    }
}
