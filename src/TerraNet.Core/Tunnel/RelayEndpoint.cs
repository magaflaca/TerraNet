using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using TerraNet.Protocol;

namespace TerraNet.Core.Tunnel;

public sealed record RelayEndpoint(string Host, int Port, string Pin)
{
    public static readonly RelayEndpoint Default = new("play.hacks.win", 9990, "al2dUqBOf1GHOZHk/tP54Z7ebTEO2C6zniM8ogjIAWU=");

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

    public async Task<SslStream> ConnectAsync(CancellationToken cancellation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(ConnectTimeout);

        var client = new TcpClient { NoDelay = true };
        try
        {
            await client.ConnectAsync(Host, Port, timeout.Token);
            var tls = new SslStream(client.GetStream(), false, (_, certificate, _, _) => CertificatePin.Matches(certificate, Pin));
            await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = Host,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }, timeout.Token);
            return tls;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }
}
