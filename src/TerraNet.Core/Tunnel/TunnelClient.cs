using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using TerraNet.Protocol;

namespace TerraNet.Core.Tunnel;

public sealed class TunnelClient(RelayEndpoint relay, DeviceCredentials credentials, string appVersion, int localPort)
{
    private static readonly TimeSpan[] Backoff = new[] { 1, 2, 4, 8, 15, 30 }.Select(s => TimeSpan.FromSeconds(s)).ToArray();
    private static readonly RejectReason[] Fatal = [RejectReason.InvalidCredentials, RejectReason.Banned, RejectReason.UnsupportedVersion];

    public event Action<TunnelState>? StateChanged;

    public TunnelState State { get; private set; } = TunnelState.Idle;

    public async Task RunAsync(CancellationToken cancellation)
    {
        var attempt = 0;
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                Publish(new TunnelState.Connecting(attempt));
                var outcome = await SessionAsync(cancellation);
                if (outcome is { } reason)
                {
                    Publish(new TunnelState.Refused(reason));
                    if (Fatal.Contains(reason))
                        return;
                }
                attempt = State is TunnelState.Online ? 0 : attempt + 1;
                await Task.Delay(Backoff[Math.Min(attempt, Backoff.Length - 1)], cancellation);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            Publish(TunnelState.Idle);
        }
    }

    private async Task<RejectReason?> SessionAsync(CancellationToken cancellation)
    {
        try
        {
            await using var control = new FrameChannel(await relay.ConnectAsync(cancellation));
            await control.SendAsync(new Hello(ProtocolInfo.Version, credentials.Device, credentials.Secret, appVersion), cancellation);

            var reply = await control.ReceiveAsync(cancellation).WaitAsync(ProtocolInfo.IdleTimeout, cancellation);
            switch (reply)
            {
                case Reject reject:
                    return reject.Reason;
                case Welcome welcome:
                    Publish(new TunnelState.Online(welcome.PublicHost, welcome.Port));
                    break;
                default:
                    return RejectReason.Unknown;
            }

            using var session = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            var keepAlive = KeepAliveAsync(control, session.Token);
            try
            {
                while (true)
                {
                    var message = await control.ReceiveAsync(session.Token).WaitAsync(ProtocolInfo.IdleTimeout, session.Token);
                    switch (message)
                    {
                        case Open open:
                            _ = BridgeAsync(open.Token, session.Token);
                            break;
                        case Ping ping:
                            await control.SendAsync(new Pong(ping.Timestamp), session.Token);
                            break;
                    }
                }
            }
            finally
            {
                await session.CancelAsync();
                await keepAlive.ContinueWith(_ => { });
            }
        }
        catch (Exception ex) when (IsTransient(ex) && !cancellation.IsCancellationRequested)
        {
            return null;
        }
    }

    private async Task BridgeAsync(StreamToken token, CancellationToken cancellation)
    {
        try
        {
            using var local = new TcpClient { NoDelay = true };
            await local.ConnectAsync(IPAddress.Loopback, localPort, cancellation);
            await using var remote = await relay.ConnectAsync(cancellation);
            await new FrameChannel(remote).SendAsync(new Attach(token), cancellation);
            await StreamPipe.RunAsync(local.GetStream(), remote, cancellation);
        }
        catch (Exception ex) when (IsTransient(ex))
        {
        }
    }

    private static async Task KeepAliveAsync(FrameChannel control, CancellationToken cancellation)
    {
        using var timer = new PeriodicTimer(ProtocolInfo.PingInterval);
        while (await timer.WaitForNextTickAsync(cancellation))
            await control.SendAsync(new Ping(Environment.TickCount64), cancellation);
    }

    private static bool IsTransient(Exception ex) =>
        ex is IOException or SocketException or AuthenticationException or ProtocolException or TimeoutException or OperationCanceledException;

    private void Publish(TunnelState state)
    {
        if (state == State)
            return;
        State = state;
        StateChanged?.Invoke(state);
    }
}
