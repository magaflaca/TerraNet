using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using TerraNet.Protocol;

namespace TerraNet.Relay;

public sealed class RelayServer(RelayOptions options, X509Certificate2 certificate)
{
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MaintenanceInterval = TimeSpan.FromHours(1);

    private readonly DeviceRegistry _registry = new(options);
    private readonly SessionTable _sessions = new(options);
    private readonly PendingStreams _pending = new();

    public async Task RunAsync(CancellationToken cancellation)
    {
        var listener = new TcpListener(IPAddress.Any, options.ListenPort);
        listener.Start();
        Log.Info($"relay listening on {options.ListenPort}, ports {options.PortRangeStart}-{options.PortRangeEnd}, pin {CertificatePin.Compute(certificate)}");
        _ = MaintainAsync(cancellation);
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellation);
                _ = HandleAsync(client, cancellation);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task MaintainAsync(CancellationToken cancellation)
    {
        using var timer = new PeriodicTimer(MaintenanceInterval);
        try
        {
            do
            {
                foreach (var device in _sessions.ActiveDevices)
                    _registry.Touch(device);
                if (_registry.Sweep(_sessions.ActivePorts) is var released and > 0)
                    Log.Info($"released {released} idle ports");
            }
            while (await timer.WaitForNextTickAsync(cancellation));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task HandleAsync(TcpClient client, CancellationToken cancellation)
    {
        var address = SessionTable.Normalize(((IPEndPoint)client.Client.RemoteEndPoint!).Address);
        var handedOff = false;
        SslStream? tls = null;
        try
        {
            client.NoDelay = true;
            tls = new SslStream(client.GetStream(), false);
            using (var handshake = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                handshake.CancelAfter(HandshakeTimeout);
                await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ServerCertificate = certificate,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                }, handshake.Token);

                var channel = new FrameChannel(tls);
                var first = await channel.ReceiveAsync(handshake.Token);
                switch (first)
                {
                    case Attach attach:
                        handedOff = _pending.Complete(attach.Token, tls);
                        return;
                    case Hello hello:
                        handedOff = true;
                        await RunSessionAsync(hello, channel, address, cancellation);
                        return;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or SocketException or AuthenticationException or ProtocolException or OperationCanceledException)
        {
        }
        finally
        {
            if (!handedOff)
            {
                tls?.Dispose();
                client.Dispose();
            }
        }
    }

    private async Task RunSessionAsync(Hello hello, FrameChannel channel, IPAddress address, CancellationToken cancellation)
    {
        await using var _ = channel;
        if (hello.Version != ProtocolInfo.Version)
        {
            await channel.SendAsync(new Reject(RejectReason.UnsupportedVersion), cancellation);
            return;
        }

        var registration = _registry.Register(hello.Device, hello.Secret.Span, _sessions.ActivePorts);
        if (registration.Error is { } error)
        {
            Log.Warn($"rejected {hello.Device} from {address}: {error}");
            await channel.SendAsync(new Reject(error), cancellation);
            return;
        }

        var session = new TunnelSession(hello.Device, address, registration.Port, channel, _pending, options);
        if (!await _sessions.TryAddAsync(session))
        {
            await channel.SendAsync(new Reject(RejectReason.TooManySessions), cancellation);
            return;
        }

        try
        {
            await session.RunAsync(cancellation);
        }
        finally
        {
            _sessions.Remove(session);
            _registry.Touch(hello.Device);
        }
    }
}
