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
    string? NativeLauncher { get; }
    bool SupportsStartOnBoot { get; }

    Task<ServerPack> PreparePackAsync(IProgress<double> progress);

    void OnSessionChanged(SessionSnapshot snapshot);

    Task<bool> ShareAsync(string text);

    Task InstallUpdateAsync(ReleaseInfo release, IProgress<double> progress);

    void PlaySound(string name, byte[] wave);
}
