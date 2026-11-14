using TerraNet.Core.Hosting;
using TerraNet.Core.Session;
using TerraNet.Core.Updates;

namespace TerraNet.App.Services;

public interface IPlatform
{
    string Id { get; }
    Version AppVersion { get; }
    string DataDirectory { get; }
    IProcessLauncher Launcher { get; }
    ServerCommand ServerCommand(ServerPack pack);
    bool SupportsStartOnBoot { get; }
    Core.Worlds.DeviceTier Tier { get; }

    Task<ServerPack> PreparePackAsync(IProgress<double> progress);

    void OnSessionChanged(SessionSnapshot snapshot);

    Task<bool> ShareAsync(string text);

    Task InstallUpdateAsync(ReleaseInfo release, IProgress<double> progress);

    void PlaySound(string name, byte[] wave);
}
