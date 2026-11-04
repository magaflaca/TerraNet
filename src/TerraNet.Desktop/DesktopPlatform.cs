using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using TerraNet.App.Services;
using TerraNet.Core.Hosting;
using TerraNet.Core.Session;
using TerraNet.Core.Updates;
using Velopack;
using Velopack.Sources;

namespace TerraNet.Desktop;

public sealed partial class DesktopPlatform : IPlatform
{
    private const string Feed = "https://terra.hacks.win/feed/windows";
    private const string DownloadPage = "https://terra.hacks.win";
    private const uint ContinuousAwake = 0x80000000 | 0x00000001;
    private const uint ContinuousOnly = 0x80000000;

    private readonly UpdateManager _updates = new(new SimpleWebSource(Feed));

    public string Id => "windows";

    public Version AppVersion =>
        _updates.CurrentVersion is { } installed
            ? new Version(installed.Major, installed.Minor, installed.Patch)
            : Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);

    public string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TerraNet");

    public IProcessLauncher Launcher { get; } = new SystemProcessLauncher();
    public string? NativeLauncher => null;
    public bool SupportsStartOnBoot => false;

    public Task<ServerPack> PreparePackAsync(IProgress<double> progress) =>
        Task.FromResult(ServerPack.Load(Path.Combine(AppContext.BaseDirectory, "server-pack")));

    public void OnSessionChanged(SessionSnapshot snapshot)
    {
        var awake = snapshot.IsActive && AppHost.Current.Settings.Current.KeepAwake;
        Dispatcher.UIThread.Post(() => SetThreadExecutionState(awake ? ContinuousAwake : ContinuousOnly));
    }

    public Task<bool> ShareAsync(string text) => Task.FromResult(false);

    public async Task InstallUpdateAsync(ReleaseInfo release, IProgress<double> progress)
    {
        if (!_updates.IsInstalled)
        {
            Process.Start(new ProcessStartInfo(DownloadPage) { UseShellExecute = true });
            return;
        }

        var update = await _updates.CheckForUpdatesAsync();
        if (update is null)
            return;
        await _updates.DownloadUpdatesAsync(update, percent => progress.Report(percent / 100.0));
        _updates.ApplyUpdatesAndRestart(update);
    }

    private readonly Dictionary<string, GCHandle> _sounds = [];

    public void PlaySound(string name, byte[] wave)
    {
        lock (_sounds)
        {
            if (!_sounds.TryGetValue(name, out var handle))
                _sounds[name] = handle = GCHandle.Alloc(wave, GCHandleType.Pinned);
            PlaySoundMemory(handle.AddrOfPinnedObject(), IntPtr.Zero, SoundMemory | SoundAsync | SoundNoDefault);
        }
    }

    private const uint SoundAsync = 0x0001;
    private const uint SoundNoDefault = 0x0002;
    private const uint SoundMemory = 0x0004;

    [LibraryImport("winmm.dll", EntryPoint = "PlaySoundW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PlaySoundMemory(IntPtr sound, IntPtr module, uint flags);

    [LibraryImport("kernel32.dll")]
    private static partial uint SetThreadExecutionState(uint flags);
}
