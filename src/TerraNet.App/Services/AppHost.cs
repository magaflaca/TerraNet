using TerraNet.Core;
using TerraNet.Core.Session;
using TerraNet.Core.Settings;
using TerraNet.Core.Updates;
using TerraNet.Core.Worlds;

namespace TerraNet.App.Services;

public sealed class AppHost
{
    private static AppHost? _current;
    private readonly TaskCompletionSource<HostSession> _session = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _preparing;

    private AppHost(IPlatform platform)
    {
        Platform = platform;
        Paths = new AppPaths(platform.DataDirectory);
        Worlds = new WorldLibrary(Paths);
        Settings = new SettingsService(Paths);
        Updates = new UpdateChecker(platform.Id, platform.AppVersion);
    }

    public static AppHost Current => _current ?? throw new InvalidOperationException("host not initialized");

    public static AppHost Initialize(IPlatform platform) => _current ??= new AppHost(platform);

    public IPlatform Platform { get; }
    public AppPaths Paths { get; }
    public WorldLibrary Worlds { get; }
    public SettingsService Settings { get; }
    public UpdateChecker Updates { get; }

    public event Action<double>? PrepareProgress;

    public HostSession? Session => _session.Task.IsCompletedSuccessfully ? _session.Task.Result : null;

    public Task<HostSession> SessionAsync()
    {
        if (Interlocked.Exchange(ref _preparing, 1) == 0)
            _ = PrepareAsync();
        return _session.Task;
    }

    private async Task PrepareAsync()
    {
        try
        {
            var pack = await Task.Run(() => Platform.PreparePackAsync(new Progress<double>(p => PrepareProgress?.Invoke(p))));
            var environment = new HostEnvironment(Paths, pack, Platform.Launcher, Platform.NativeLauncher, Platform.AppVersion.ToString(3), Worlds, Settings);
            var session = new HostSession(environment);
            session.Changed += Platform.OnSessionChanged;
            _session.TrySetResult(session);
        }
        catch (Exception ex)
        {
            _session.TrySetException(ex);
        }
    }
}
