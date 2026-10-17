using System.Net;
using System.Net.Sockets;
using TerraNet.Protocol;

namespace TerraNet.Relay;

public sealed class TunnelSession(
    DeviceId device,
    IPAddress address,
    int port,
    FrameChannel control,
    PendingStreams pending,
    RelayOptions options) : IAsyncDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private readonly TaskCompletionSource _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _players;

    public DeviceId Device => device;
    public IPAddress Address => address;
    public int Port => port;

    public async Task RunAsync(CancellationToken cancellation)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _lifetime.Token);
        var listener = new TcpListener(IPAddress.Any, port);
        try
        {
            listener.Start();
            await control.SendAsync(new Welcome((ushort)port, options.PublicHost), linked.Token);
            Log.Info($"session {device} online on {port} from {address}");

            var tasks = new[] { AcceptLoopAsync(listener, linked.Token), ControlLoopAsync(linked.Token), KeepAliveAsync(linked.Token) };
            await Task.WhenAny(tasks);
        }
        catch (Exception ex) when (ex is IOException or SocketException or ProtocolException or OperationCanceledException or TimeoutException)
        {
        }
        finally
        {
            await linked.CancelAsync();
            listener.Stop();
            await control.DisposeAsync();
            Log.Info($"session {device} closed on {port}");
            _finished.TrySetResult();
        }
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellation)
    {
        while (!cancellation.IsCancellationRequested)
        {
            var player = await listener.AcceptTcpClientAsync(cancellation);
            if (Interlocked.Increment(ref _players) > options.MaxPlayersPerSession)
            {
                Interlocked.Decrement(ref _players);
                player.Dispose();
                continue;
            }
            _ = ServePlayerAsync(player, cancellation);
        }
    }

    private async Task ServePlayerAsync(TcpClient player, CancellationToken cancellation)
    {
        try
        {
            using (player)
            {
                player.NoDelay = true;
                var token = StreamToken.New();
                var waiting = pending.WaitAsync(token, ProtocolInfo.AttachTimeout, cancellation);
                await control.SendAsync(new Open(token), cancellation);
                var stream = await waiting;
                if (stream is null)
                    return;
                await using (stream)
                    await StreamPipe.RunAsync(player.GetStream(), stream, cancellation);
            }
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException)
        {
        }
        finally
        {
            Interlocked.Decrement(ref _players);
        }
    }

    private async Task ControlLoopAsync(CancellationToken cancellation)
    {
        while (!cancellation.IsCancellationRequested)
        {
            var message = await control.ReceiveAsync(cancellation).WaitAsync(ProtocolInfo.IdleTimeout, cancellation);
            if (message is Ping ping)
                await control.SendAsync(new Pong(ping.Timestamp), cancellation);
        }
    }

    private async Task KeepAliveAsync(CancellationToken cancellation)
    {
        using var timer = new PeriodicTimer(ProtocolInfo.PingInterval);
        while (await timer.WaitForNextTickAsync(cancellation))
            await control.SendAsync(new Ping(Environment.TickCount64), cancellation);
    }

    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync();
        await _finished.Task.WaitAsync(TimeSpan.FromSeconds(5)).ContinueWith(_ => { });
        _lifetime.Dispose();
    }
}
