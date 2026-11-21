using System.IO.Compression;
using Android.Content;
using Android.Media;
using AndroidX.Core.Content;
using TerraNet.App.Services;
using TerraNet.Core.Hosting;
using TerraNet.Core.Session;
using TerraNet.Core.Updates;
using AndroidApp = Android.App.Application;

namespace TerraNet.Android;

public sealed class AndroidPlatform(AndroidApp context) : IPlatform
{
    private const string PackAsset = "server.zip";
    private const string Authority = "win.hacks.terranet.files";

    private readonly Lazy<SoundPool> _pool = new(() => new SoundPool.Builder()
        .SetMaxStreams(4)!
        .SetAudioAttributes(new AudioAttributes.Builder().SetUsage(AudioUsageKind.Game)!.SetContentType(AudioContentType.Sonification)!.Build())!
        .Build()!);
    private readonly Dictionary<string, int> _sounds = [];
    private int _askedExemption;

    public string Id => "android";

    public Version AppVersion
    {
        get
        {
            var info = context.PackageManager!.GetPackageInfo(context.PackageName!, 0)!;
            return Version.TryParse(info.VersionName, out var version) ? version : new Version(0, 0, 0);
        }
    }

    public string DataDirectory => context.FilesDir!.AbsolutePath;
    public IProcessLauncher Launcher { get; } = new SystemProcessLauncher();
    public ServerCommand ServerCommand(ServerPack pack) =>
        new(Path.Combine(context.ApplicationInfo!.NativeLibraryDir!, "libtnhost.so"), [pack.DotnetRoot!, pack.Entry]);
    public bool SupportsStartOnBoot => true;
    public bool SupportsTray => false;

    public void ApplyStartOnBoot(bool enabled)
    {
    }

    public Core.Worlds.DeviceTier Tier => Core.Worlds.DeviceTiers.Measure(TotalMemory(), Environment.ProcessorCount, true);

    private long TotalMemory()
    {
        var info = new global::Android.App.ActivityManager.MemoryInfo();
        (context.GetSystemService(global::Android.Content.Context.ActivityService) as global::Android.App.ActivityManager)?.GetMemoryInfo(info);
        return info.TotalMem;
    }

    public async Task<ServerPack> PreparePackAsync(IProgress<double> progress)
    {
        var info = context.PackageManager!.GetPackageInfo(context.PackageName!, 0)!;
        var stamp = $"{info.VersionName}-{info.LastUpdateTime}";
        var root = Path.Combine(context.NoBackupFilesDir!.AbsolutePath, "pack");
        var marker = Path.Combine(root, ".stamp");
        if (File.Exists(marker) && await File.ReadAllTextAsync(marker) == stamp)
            return ServerPack.Load(root);

        if (Directory.Exists(root))
            Directory.Delete(root, true);
        Directory.CreateDirectory(root);

        var archive = Path.Combine(context.CacheDir!.AbsolutePath, PackAsset);
        await using (var asset = context.Assets!.Open(PackAsset))
        await using (var file = File.Create(archive))
            await asset.CopyToAsync(file);

        using (var zip = ZipFile.OpenRead(archive))
        {
            var total = zip.Entries.Count;
            var done = 0;
            foreach (var entry in zip.Entries)
            {
                var target = Path.GetFullPath(Path.Combine(root, entry.FullName));
                if (!target.StartsWith(root, StringComparison.Ordinal))
                    continue;
                if (entry.FullName.EndsWith('/'))
                    Directory.CreateDirectory(target);
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    entry.ExtractToFile(target, true);
                }
                progress.Report(++done / (double)total);
            }
        }
        File.Delete(archive);
        await File.WriteAllTextAsync(marker, stamp);
        return ServerPack.Load(root);
    }

    public void OnSessionChanged(SessionSnapshot snapshot)
    {
        if (!snapshot.IsActive)
            return;
        HostService.Ensure(context);
        if (Interlocked.Exchange(ref _askedExemption, 1) == 0 && MainActivity.Current is { } activity)
            activity.RunOnUiThread(activity.RequestBackgroundExemption);
    }

    public Task<bool> ShareAsync(string text)
    {
        var send = new Intent(Intent.ActionSend).SetType("text/plain")!.PutExtra(Intent.ExtraText, text)!;
        var chooser = Intent.CreateChooser(send, (string?)null)!.AddFlags(ActivityFlags.NewTask)!;
        context.StartActivity(chooser);
        return Task.FromResult(true);
    }

    public async Task InstallUpdateAsync(ReleaseInfo release, IProgress<double> progress)
    {
        if (!context.PackageManager!.CanRequestPackageInstalls())
        {
            var settings = new Intent(global::Android.Provider.Settings.ActionManageUnknownAppSources, global::Android.Net.Uri.Parse("package:" + context.PackageName))
                .AddFlags(ActivityFlags.NewTask)!;
            context.StartActivity(settings);
            return;
        }

        var directory = Path.Combine(context.CacheDir!.AbsolutePath, "updates");
        var apk = await UpdateChecker.DownloadAsync(release, directory, progress);
        var uri = FileProvider.GetUriForFile(context, Authority, new Java.IO.File(apk))!;
        var install = new Intent(Intent.ActionView)
            .SetDataAndType(uri, "application/vnd.android.package-archive")!
            .AddFlags(ActivityFlags.NewTask | ActivityFlags.GrantReadUriPermission)!;
        context.StartActivity(install);
    }

    public void PlaySound(string name, byte[] wave)
    {
        lock (_sounds)
        {
            if (!_sounds.TryGetValue(name, out var id))
            {
                var path = Path.Combine(context.CacheDir!.AbsolutePath, $"sfx-{name}.wav");
                File.WriteAllBytes(path, wave);
                id = _pool.Value.Load(path, 1);
                _sounds[name] = id;
                _pool.Value.LoadComplete += (_, e) =>
                {
                    if (e.SampleId == id)
                        _pool.Value.Play(id, 0.8f, 0.8f, 1, 0, 1);
                };
                return;
            }
            _pool.Value.Play(id, 0.8f, 0.8f, 1, 0, 1);
        }
    }
}
