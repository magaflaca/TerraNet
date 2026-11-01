using TerraNet.Core.Hosting;
using TerraNet.Core.Identity;
using TerraNet.Core.Settings;
using TerraNet.Core.Tunnel;
using TerraNet.Core.Worlds;

namespace TerraNet.Core.Session;

public sealed class HostEnvironment(
    AppPaths paths,
    ServerPack pack,
    IProcessLauncher launcher,
    string? nativeLauncher,
    string appVersion,
    WorldLibrary worlds,
    SettingsService settings)
{
    public AppPaths Paths { get; } = paths;
    public ServerPack Pack { get; } = pack;
    public IProcessLauncher Launcher { get; } = launcher;
    public string? NativeLauncher { get; } = nativeLauncher;
    public string AppVersion { get; } = appVersion;
    public WorldLibrary Worlds { get; } = worlds;
    public SettingsService Settings { get; } = settings;
    public IdentityStore Identity { get; } = new(paths);
    public RelayEndpoint Relay { get; init; } = RelayEndpoint.Default;
}
