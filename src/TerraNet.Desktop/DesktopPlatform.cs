using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using TerraNet.App.Services;
using TerraNet.Core.Hosting;
using TerraNet.Core.Session;
using TerraNet.Core.Updates;

namespace TerraNet.Desktop;

public sealed partial class DesktopPlatform : IPlatform
{
    private const string PackResource = "TerraNet.ServerPack.zip";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "TerraNet";
    private const uint ContinuousAwake = 0x80000000 | 0x00000001;
    private const uint ContinuousOnly = 0x80000000;
    private const uint SoundAsync = 0x0001;
    private const uint SoundNoDefault = 0x0002;
    private const uint SoundMemory = 0x0004;

    private readonly Dictionary<string, GCHandle> _sounds = [];

    public string Id => "windows";

    public Version AppVersion { get; } = Assembly.GetEntryAssembly()?.GetName().Version is { } v ? new Version(v.Major, v.Minor, v.Build) : new Version(0, 0, 0);

    public string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TerraNet");

    public IProcessLauncher Launcher { get; } = new SystemProcessLauncher();
    public bool SupportsStartOnBoot => true;
    public bool SupportsTray => true;

    public void ApplyStartOnBoot(bool enabled)
    {
        using var run = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            run.SetValue(RunValue, $"\"{Environment.ProcessPath}\" {Program.BackgroundFlag}");
        else
            run.DeleteValue(RunValue, false);
    }

    public Core.Worlds.DeviceTier Tier { get; } =
        Core.Worlds.DeviceTiers.Measure(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes, Environment.ProcessorCount, false);

    public ServerCommand ServerCommand(ServerPack pack) =>
        new(Environment.ProcessPath!, [ServerMode.Flag, pack.ServerDirectory]);

    public async Task<ServerPack> PreparePackAsync(IProgress<double> progress)
    {
        await using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(PackResource)
                                   ?? throw new InvalidOperationException("server pack missing");
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TerraNet", "pack", $"{AppVersion}-{resource.Length}");
        if (File.Exists(Path.Combine(root, ServerPack.ManifestFile)))
            return ServerPack.Load(root);

        var staging = root + ".tmp";
        if (Directory.Exists(staging))
            Directory.Delete(staging, true);
        using (var zip = new ZipArchive(resource, ZipArchiveMode.Read))
        {
            var done = 0;
            foreach (var entry in zip.Entries)
            {
                var target = Path.GetFullPath(Path.Combine(staging, entry.FullName));
                if (entry.FullName.EndsWith('/'))
                    Directory.CreateDirectory(target);
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    entry.ExtractToFile(target, true);
                }
                progress.Report(++done / (double)zip.Entries.Count);
            }
        }
        Directory.Move(staging, root);
        return ServerPack.Load(root);
    }

    public void OnSessionChanged(SessionSnapshot snapshot)
    {
        var awake = snapshot.IsActive && AppHost.Current.Settings.Current.KeepAwake;
        Dispatcher.UIThread.Post(() => SetThreadExecutionState(awake ? ContinuousAwake : ContinuousOnly));
    }

    public Task<bool> ShareAsync(string text) => Task.FromResult(false);

    public async Task InstallUpdateAsync(ReleaseInfo release, IProgress<double> progress)
    {
        var folder = Path.Combine(Path.GetTempPath(), "TerraNet");
        var downloaded = await UpdateChecker.DownloadAsync(release, folder, progress);
        var current = Environment.ProcessPath!;
        var script = Path.Combine(folder, "update.cmd");
        await File.WriteAllTextAsync(script, string.Join("\r\n",
            "@echo off",
            ":wait",
            $"tasklist /FI \"PID eq {Environment.ProcessId}\" | find \"{Environment.ProcessId}\" >nul && (timeout /t 1 /nobreak >nul & goto wait)",
            "set tries=0",
            ":move",
            $"move /Y \"{downloaded}\" \"{current}\" >nul 2>&1 && goto done",
            "set /a tries+=1",
            "if %tries% geq 60 goto done",
            "timeout /t 1 /nobreak >nul",
            "goto move",
            ":done",
            $"start \"\" \"{current}\"",
            "del \"%~f0\""));
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"") { CreateNoWindow = true, UseShellExecute = false });
        Dispatcher.UIThread.Post(() => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown());
    }

    public void PlaySound(string name, byte[] wave)
    {
        lock (_sounds)
        {
            if (!_sounds.TryGetValue(name, out var handle))
                _sounds[name] = handle = GCHandle.Alloc(wave, GCHandleType.Pinned);
            PlaySoundMemory(handle.AddrOfPinnedObject(), IntPtr.Zero, SoundMemory | SoundAsync | SoundNoDefault);
        }
    }

    [LibraryImport("winmm.dll", EntryPoint = "PlaySoundW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PlaySoundMemory(IntPtr sound, IntPtr module, uint flags);

    [LibraryImport("kernel32.dll")]
    private static partial uint SetThreadExecutionState(uint flags);
}
