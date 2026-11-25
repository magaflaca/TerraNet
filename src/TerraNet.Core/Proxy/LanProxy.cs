using System.Net;
using System.Net.Sockets;
using TerraNet.Core.Diagnostics;

namespace TerraNet.Core.Proxy;

public enum ProxyStatus
{
    Idle,
    Probing,
    Running,
    Failed
}

public sealed record ProxySnapshot(
    ProxyStatus Status,
    string Host,
    int Port,
    RemoteWorld? World,
    int LocalPort,
    string? LocalAddress,
    int Clients,
    bool Translating,
    ProbeError Error,
    bool Unreachable)
{
    public static readonly ProxySnapshot Idle = new(ProxyStatus.Idle, "", 0, null, 0, null, 0, false, ProbeError.None, false);

    public bool IsActive => Status is ProxyStatus.Probing or ProxyStatus.Running;
}

public sealed class LanProxy
{
    public const int PreferredPort = 7777;
    private const int PortRange = 20;
    private const int BufferSize = 16 * 1024;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(8);

    private readonly Lock _gate = new();
    private CancellationTokenSource? _run;
    private int _generation;
    private int _clients;
    private int _translating;

    public event Action<ProxySnapshot>? Changed;

    public ProxySnapshot Snapshot { get; private set; } = ProxySnapshot.Idle;

    public void Start(string host, int port)
    {
        CancellationTokenSource run;
        int generation;
        lock (_gate)
        {
            _run?.Cancel();
            _run?.Dispose();
            run = _run = new CancellationTokenSource();
            generation = ++_generation;
            _clients = 0;
            _translating = 0;
            Snapshot = ProxySnapshot.Idle with { Status = ProxyStatus.Probing, Host = host, Port = port };
        }
        Changed?.Invoke(Snapshot);
        _ = RunAsync(generation, host, port, run.Token);
    }

    public void Stop()
    {
        lock (_gate)
        {
            _run?.Cancel();
            _run?.Dispose();
            _run = null;
            _generation++;
            Snapshot = ProxySnapshot.Idle;
        }
        Changed?.Invoke(ProxySnapshot.Idle);
    }

    private async Task RunAsync(int generation, string host, int port, CancellationToken cancellation)
    {
        try
        {
            var probe = await RemoteServer.ProbeAsync(host, port, cancellation);
            if (!probe.Success)
            {
                Update(generation, s => s with { Status = ProxyStatus.Failed, Error = probe.Error });
                return;
            }

            using var listener = Listen(out var localPort);
            var translator = new LegacyTranslator();
            Update(generation, s => s with
            {
                Status = ProxyStatus.Running,
                World = probe.World,
                LocalPort = localPort,
                LocalAddress = LanAnnouncer.LocalAddress()
            });
            AppLog.Write($"proxy {host}:{port} on {localPort} modern={probe.World!.Modern}");

            var announce = LanAnnouncer.RunAsync(() => LanAnnouncer.Build(Snapshot.World ?? probe.World!, localPort, PlayerCount()), cancellation);
            var refresh = RefreshAsync(generation, host, port, cancellation);
            while (!cancellation.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellation);
                _ = ServeAsync(generation, client, host, port, translator, cancellation);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (SocketException ex)
        {
            AppLog.Error("proxy", ex);
            if (!cancellation.IsCancellationRequested)
                Update(generation, s => s with { Status = ProxyStatus.Failed, Error = ProbeError.None });
        }
    }

    private int PlayerCount()
    {
        var snapshot = Snapshot;
        return snapshot.World is { PlayersKnown: true } world ? world.Players : snapshot.Clients;
    }

    private static TcpListener Listen(out int port)
    {
        for (var candidate = PreferredPort; candidate < PreferredPort + PortRange; candidate++)
        {
            if (!LoopbackFree(candidate))
                continue;
            var listener = new TcpListener(IPAddress.Any, candidate);
            try
            {
                listener.Start();
                port = candidate;
                return listener;
            }
            catch (SocketException)
            {
                listener.Stop();
            }
        }
        var fallback = new TcpListener(IPAddress.Any, 0);
        fallback.Start();
        port = ((IPEndPoint)fallback.LocalEndpoint).Port;
        return fallback;
    }

    private static bool LoopbackFree(int port)
    {
        try
        {
            using var probe = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { ExclusiveAddressUse = true };
            probe.Bind(new IPEndPoint(IPAddress.Loopback, port));
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private async Task RefreshAsync(int generation, string host, int port, CancellationToken cancellation)
    {
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                await Task.Delay(RefreshInterval, cancellation);
                if (Snapshot.World is not { Modern: true })
                    continue;
                var probe = await RemoteServer.ProbeAsync(host, port, cancellation);
                if (probe.World is { Modern: true } world)
                    Update(generation, s => s with { World = world, Unreachable = false });
                else if (probe.Error == ProbeError.Unreachable)
                    Update(generation, s => s with { Unreachable = true });
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ServeAsync(int generation, TcpClient console, string host, int port, LegacyTranslator translator, CancellationToken cancellation)
    {
        using var _ = console;
        using var remote = new TcpClient { NoDelay = true };
        console.NoDelay = true;
        try
        {
            using (var connect = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                connect.CancelAfter(ConnectTimeout);
                await remote.ConnectAsync(host, port, connect.Token);
            }
        }
        catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException && !cancellation.IsCancellationRequested)
        {
            Update(generation, s => s with { Unreachable = true });
            return;
        }

        var translate = false;
        Adjust(generation, ref _clients, 1);
        try
        {
            var consoleStream = console.GetStream();
            var remoteStream = remote.GetStream();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            var consoleFrames = new PacketReader(consoleStream);
            var hello = await consoleFrames.ReadFrameAsync(linked.Token);
            if (hello is null)
                return;
            translate = Snapshot.World is { Modern: false } && LegacyTranslator.IsModernHello(hello);
            if (translate)
            {
                Adjust(generation, ref _translating, 1);
                hello = LegacyTranslator.LegacyHello();
            }
            await remoteStream.WriteAsync(hello, linked.Token);
            if (Snapshot.Unreachable)
                Update(generation, s => s with { Unreachable = false });

            var upstream = translate
                ? RelayFramesAsync(consoleFrames, remoteStream, frame => { translator.FromClient(frame); return frame; }, linked.Token)
                : CopyAsync(consoleStream, remoteStream, linked.Token);
            var downstream = translate
                ? RelayFramesAsync(new PacketReader(remoteStream), consoleStream, translator.FromServer, linked.Token)
                : WatchServerAsync(generation, remoteStream, consoleStream, linked.Token);
            await Task.WhenAny(upstream, downstream);
            await linked.CancelAsync();
            await Task.WhenAll(Quiet(upstream), Quiet(downstream));
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or InvalidDataException)
        {
        }
        finally
        {
            if (translate)
                Adjust(generation, ref _translating, -1);
            Adjust(generation, ref _clients, -1);
        }
    }

    private async Task WatchServerAsync(int generation, NetworkStream remote, NetworkStream console, CancellationToken cancellation)
    {
        var frames = new PacketReader(remote);
        var first = await frames.ReadFrameAsync(cancellation);
        if (first is null)
            return;
        if (first[2] == 163 && RemoteServer.ParseServerInfo(first[3..]) is { } world)
            Update(generation, s => s with { World = world });
        await console.WriteAsync(first, cancellation);
        await CopyAsync(remote, console, cancellation);
    }

    private static async Task RelayFramesAsync(PacketReader source, Stream destination, Func<byte[], byte[]> transform, CancellationToken cancellation)
    {
        while (await source.ReadFrameAsync(cancellation) is { } frame)
            await destination.WriteAsync(transform(frame), cancellation);
    }

    private static async Task CopyAsync(Stream source, Stream destination, CancellationToken cancellation)
    {
        var buffer = new byte[BufferSize];
        int read;
        while ((read = await source.ReadAsync(buffer, cancellation)) > 0)
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellation);
    }

    private static async Task Quiet(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
        }
    }

    private void Adjust(int generation, ref int counter, int delta)
    {
        Interlocked.Add(ref counter, delta);
        Update(generation, s => s with { Clients = Math.Max(0, Volatile.Read(ref _clients)), Translating = Volatile.Read(ref _translating) > 0 });
    }

    private void Update(int generation, Func<ProxySnapshot, ProxySnapshot> change)
    {
        ProxySnapshot snapshot;
        lock (_gate)
        {
            if (generation != _generation)
                return;
            snapshot = Snapshot = change(Snapshot);
        }
        Changed?.Invoke(snapshot);
    }
}
