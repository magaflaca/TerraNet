using System.Net;
using System.Net.Sockets;
using TerraNet.Core.Hosting;
using TerraNet.Core.Identity;
using TerraNet.Core.Settings;
using TerraNet.Core.Tunnel;
using TerraNet.Core.Worlds;

namespace TerraNet.Core.Session;

public sealed record SessionSnapshot(ServerState Server, TunnelState Tunnel, WorldEntry? World, DateTimeOffset? StartedAt)
{
    public static readonly SessionSnapshot Idle = new(ServerState.Idle, TunnelState.Idle, null, null);

    public bool IsActive => Server.IsActive;

    public string? PublicAddress => Tunnel is TunnelState.Online online ? online.Address : null;
}

public sealed class HostSession
{
    private const int PreferredPort = 7777;
    private const int MaxCrashRestarts = 3;
    private static readonly TimeSpan CrashWindow = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(3);

    private readonly HostEnvironment _environment;
    private readonly Lock _gate = new();
    private readonly List<DateTimeOffset> _crashes = [];
    private CancellationTokenSource? _run;
    private TunnelClient? _tunnel;
    private bool _stopping;

    public HostSession(HostEnvironment environment)
    {
        _environment = environment;
        Server = new ServerHost(environment.Launcher, Path.Combine(environment.Paths.ServerWorkspace, "server.pid"));
        Server.StateChanged += _ => Refresh();
    }

    public event Action<SessionSnapshot>? Changed;

    public ServerHost Server { get; }

    public SessionSnapshot Snapshot { get; private set; } = SessionSnapshot.Idle;

    public bool Start(string worldId)
    {
        var world = _environment.Worlds.Find(worldId) ?? throw new ArgumentException("unknown world", nameof(worldId));
        var port = FreePort();
        var first = BuildSpec(world, port);

        CancellationTokenSource run;
        lock (_gate)
        {
            if (_run is not null && !Server.State.IsActive)
            {
                _run.Cancel();
                _run.Dispose();
                _run = null;
                _tunnel = null;
            }
            if (_run is not null)
                return false;
            _stopping = false;
            _crashes.Clear();
            run = _run = new CancellationTokenSource();
            Snapshot = Snapshot with { World = world, StartedAt = DateTimeOffset.UtcNow };
        }

        _environment.Settings.Update(s => s with { LastWorldId = worldId });
        _environment.Worlds.MarkPlayed(worldId);
        StartTunnel(port, run.Token);
        _ = RunServerAsync(world, port, first, run.Token);
        return true;
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? run;
        lock (_gate)
        {
            run = _run;
            if (run is null)
                return;
            _stopping = true;
        }

        await Server.StopAsync();
        await run.CancelAsync();
        lock (_gate)
        {
            _run = null;
            _tunnel = null;
            run.Dispose();
            Snapshot = SessionSnapshot.Idle;
        }
        Refresh();
    }

    private void StartTunnel(int port, CancellationToken cancellation)
    {
        var tunnel = new TunnelClient(_environment.Relay, _environment.Identity.Load(), _environment.AppVersion, port);
        tunnel.StateChanged += _ => Refresh();
        _tunnel = tunnel;
        _ = tunnel.RunAsync(cancellation);
    }

    private ProcessSpec BuildSpec(WorldEntry world, int port)
    {
        var current = _environment.Worlds.Find(world.Profile.Id) ?? world;
        return LaunchPlan.Build(_environment.Pack, _environment.Paths, new ServerLaunch(current, _environment.Settings.Current, port), _environment.Command);
    }

    private async Task RunServerAsync(WorldEntry world, int port, ProcessSpec spec, CancellationToken cancellation)
    {
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                await Server.StartAsync(spec);
                if (_stopping || Server.State is not ServerState.Crashed || !ShouldRestart())
                    break;
                await Task.Delay(RestartDelay, cancellation);
                spec = BuildSpec(world, port);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Server.ReportFailure(ex);
            return;
        }
        finally
        {
            if (!_stopping && Server.State is not ServerState.Crashed)
                await StopAsync();
        }
    }

    private bool ShouldRestart()
    {
        if (!_environment.Settings.Current.RestartOnCrash)
            return false;
        var now = DateTimeOffset.UtcNow;
        _crashes.RemoveAll(c => now - c > CrashWindow);
        _crashes.Add(now);
        return _crashes.Count <= MaxCrashRestarts;
    }

    private void Refresh()
    {
        SessionSnapshot snapshot;
        lock (_gate)
        {
            snapshot = Snapshot with
            {
                Server = Server.State,
                Tunnel = _tunnel?.State ?? TunnelState.Idle
            };
            Snapshot = snapshot;
        }
        Changed?.Invoke(snapshot);
    }

    private static int FreePort()
    {
        foreach (var candidate in new[] { PreferredPort, 0 })
        {
            try
            {
                using var probe = new TcpListener(IPAddress.Loopback, candidate);
                probe.Start();
                return ((IPEndPoint)probe.LocalEndpoint).Port;
            }
            catch (SocketException)
            {
            }
        }
        throw new InvalidOperationException("no free local port");
    }
}
