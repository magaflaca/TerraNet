using TerraNet.Agent;
using TerraNet.Core.Settings;
using TerraNet.Core.Worlds;

namespace TerraNet.Core.Hosting;

public sealed record ServerLaunch(WorldEntry World, HostSettings Settings, int Port);

public static class LaunchPlan
{
    public static ProcessSpec Build(ServerPack pack, AppPaths paths, ServerLaunch launch, string? nativeLauncher)
    {
        var workspace = paths.ServerWorkspace;
        var configDirectory = Path.Combine(workspace, "tshock");
        TShockConfig.Write(configDirectory, launch.Settings);

        var arguments = WorldArguments(launch)
            .Concat(
            [
                "-ip", "127.0.0.1",
                "-port", launch.Port.ToString(),
                "-maxplayers", launch.Settings.MaxPlayers.ToString(),
                "-noupnp",
                "-configpath", configDirectory,
                "-logpath", Path.Combine(workspace, "logs"),
                "-savedirectory", paths.TerrariaSave
            ])
            .ToList();

        var environment = new Dictionary<string, string>(pack.Environment)
        {
            [AgentWire.SaveDirectoryVariable] = paths.TerrariaSave,
            ["DOTNET_STARTUP_HOOKS"] = pack.StartupHook,
            ["HOME"] = paths.TerrariaSave,
            ["DOTNET_gcServer"] = "0",
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
        };

        if (pack.Host is { } host)
        {
            environment["DOTNET_ROOT"] = pack.DotnetRoot;
            return new ProcessSpec(host, arguments, workspace, environment);
        }

        if (nativeLauncher is null)
            throw new InvalidOperationException("this server pack requires a native launcher");
        return new ProcessSpec(nativeLauncher, [pack.DotnetRoot, pack.Entry, .. arguments], workspace, environment);
    }

    private static IEnumerable<string> WorldArguments(ServerLaunch launch)
    {
        var profile = launch.World.Profile;
        yield return "-world";
        yield return launch.World.FilePath;
        yield return "-worldname";
        yield return profile.Name;
        yield return "-autocreate";
        yield return ((int)profile.Size).ToString();
        yield return "-difficulty";
        yield return ((int)profile.Difficulty).ToString();
        yield return "-worldevil";
        yield return profile.Evil switch
        {
            WorldEvil.Corruption => "corrupt",
            WorldEvil.Crimson => "crimson",
            _ => "random"
        };
        if (!string.IsNullOrWhiteSpace(profile.Seed))
        {
            yield return "-seed";
            yield return profile.Seed.Trim();
        }
    }
}
