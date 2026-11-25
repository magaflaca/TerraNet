using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace TerraNet.Core.Proxy;

public static class LanAnnouncer
{
    public const int BroadcastPort = 8888;
    private const int Magic = 1010;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    public static byte[] Build(RemoteWorld world, int port, int players)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);
        writer.Write(Magic);
        writer.Write(port);
        writer.Write(world.Name);
        writer.Write(string.Empty);
        writer.Write(world.Width);
        writer.Write(world.Crimson);
        writer.Write(world.GameMode);
        writer.Write(world.MaxPlayers);
        writer.Write((byte)Math.Clamp(players, 0, 255));
        writer.Write(world.HardMode);
        writer.Flush();
        return stream.ToArray();
    }

    public static async Task RunAsync(Func<byte[]> payload, CancellationToken cancellation)
    {
        while (!cancellation.IsCancellationRequested)
        {
            Send(payload());
            await Task.Delay(Interval, cancellation);
        }
    }

    private static void Send(byte[] data)
    {
        var any = new IPEndPoint(IPAddress.Broadcast, BroadcastPort);
        foreach (var (address, broadcast) in Interfaces())
        {
            try
            {
                using var udp = new UdpClient(new IPEndPoint(address, 0)) { EnableBroadcast = true };
                udp.Send(data, data.Length, any);
                if (broadcast is not null && !broadcast.Equals(IPAddress.Broadcast))
                    udp.Send(data, data.Length, new IPEndPoint(broadcast, BroadcastPort));
            }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
            {
            }
        }
    }

    public static IEnumerable<(IPAddress Address, IPAddress? Broadcast)> Interfaces()
    {
        NetworkInterface[] interfaces;
        try
        {
            interfaces = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (NetworkInformationException)
        {
            yield break;
        }
        foreach (var nic in interfaces)
        {
            if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;
            IEnumerable<UnicastIPAddressInformation> addresses;
            try
            {
                addresses = nic.GetIPProperties().UnicastAddresses.ToList();
            }
            catch (NetworkInformationException)
            {
                continue;
            }
            foreach (var unicast in addresses)
            {
                var address = unicast.Address;
                if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address) || IsLinkLocal(address))
                    continue;
                yield return (address, DirectedBroadcast(address, unicast));
            }
        }
    }

    public static string? LocalAddress()
    {
        var all = Interfaces().Select(i => i.Address).ToList();
        return (all.FirstOrDefault(IsPrivate) ?? all.FirstOrDefault())?.ToString();
    }

    private static IPAddress? DirectedBroadcast(IPAddress address, UnicastIPAddressInformation unicast)
    {
        int prefix;
        try
        {
            prefix = unicast.PrefixLength;
        }
        catch (Exception ex) when (ex is NotImplementedException or PlatformNotSupportedException)
        {
            return null;
        }
        if (prefix is <= 0 or >= 31)
            return null;
        var bytes = address.GetAddressBytes();
        var value = (uint)(bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3]);
        value |= uint.MaxValue >> prefix;
        return new IPAddress([(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value]);
    }

    private static bool IsLinkLocal(IPAddress address) => address.GetAddressBytes() is [169, 254, ..];

    private static bool IsPrivate(IPAddress address) => address.GetAddressBytes() switch
    {
        [10, ..] => true,
        [172, var b, ..] when b is >= 16 and <= 31 => true,
        [192, 168, ..] => true,
        _ => false
    };
}
