using System.Net.Sockets;
using System.Text;

namespace TerraNet.Core.Proxy;

public sealed record RemoteWorld(
    string Name,
    ushort Width,
    bool Crimson,
    int GameMode,
    byte MaxPlayers,
    byte Players,
    bool Modern);

public sealed record ProbeResult(bool Reachable, RemoteWorld? Info);

public static class RemoteServer
{
    public const string ModernVersion = "Terraria329";
    public const string LegacyVersion = "Terraria326";
    public const byte ServerInfo = 163;

    private const byte DefaultMaxPlayers = 16;
    private const ushort SmallWidth = 4200;

    public static readonly TimeSpan GreetingWait = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(6);

    public static RemoteWorld Unknown(string host) => new(host, SmallWidth, false, 0, DefaultMaxPlayers, 0, false);

    public static async Task<ProbeResult> ProbeAsync(string host, int port, CancellationToken cancellation)
    {
        using var client = new TcpClient { NoDelay = true };
        try
        {
            using var connect = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            connect.CancelAfter(ConnectTimeout);
            await client.ConnectAsync(host, port, connect.Token);
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException or IOException && !cancellation.IsCancellationRequested)
        {
            return new ProbeResult(false, null);
        }

        try
        {
            if (!await WaitForDataAsync(client, GreetingWait, cancellation))
                return new ProbeResult(true, null);
            var first = await new PacketReader(client.GetStream()).ReadFrameAsync(cancellation);
            return new ProbeResult(true, first is { } frame && frame[2] == ServerInfo ? ParseServerInfo(frame[3..]) : null);
        }
        catch (Exception ex) when (ex is IOException or SocketException or InvalidDataException or ObjectDisposedException)
        {
            return new ProbeResult(true, null);
        }
    }

    public static async Task<bool> WaitForDataAsync(TcpClient client, TimeSpan wait, CancellationToken cancellation)
    {
        var deadline = DateTime.UtcNow + wait;
        while (client.Available == 0)
        {
            if (DateTime.UtcNow >= deadline || !client.Connected)
                return false;
            await Task.Delay(50, cancellation);
        }
        return true;
    }

    public static RemoteWorld? ParseServerInfo(byte[] body)
    {
        try
        {
            using var reader = new BinaryReader(new MemoryStream(body), Encoding.UTF8);
            reader.ReadInt32();
            var name = reader.ReadString();
            reader.ReadString();
            var width = reader.ReadUInt16();
            var crimson = reader.ReadBoolean();
            var mode = reader.ReadInt32();
            var max = reader.ReadByte();
            var players = reader.ReadByte();
            return new RemoteWorld(name, width, crimson, mode, max, players, true);
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException or ArgumentException)
        {
            return null;
        }
    }
}

public sealed record Packet(byte Id, byte[] Body)
{
    public static byte[] Build(byte id, Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);
        writer.Write((ushort)0);
        writer.Write(id);
        write(writer);
        writer.Flush();
        var data = stream.ToArray();
        BitConverter.TryWriteBytes(data.AsSpan(0, 2), (ushort)data.Length);
        return data;
    }
}

public sealed class PacketReader(Stream stream)
{
    private readonly byte[] _header = new byte[2];

    public async Task<Packet?> ReadAsync(CancellationToken cancellation)
    {
        var frame = await ReadFrameAsync(cancellation);
        return frame is null ? null : new Packet(frame[2], frame[3..]);
    }

    public async Task<byte[]?> ReadFrameAsync(CancellationToken cancellation)
    {
        if (!await FillAsync(_header, cancellation))
            return null;
        var length = BitConverter.ToUInt16(_header);
        if (length < 3)
            throw new InvalidDataException("bad packet length");
        var frame = new byte[length];
        _header.CopyTo(frame, 0);
        if (!await FillAsync(frame.AsMemory(2), cancellation))
            throw new EndOfStreamException();
        return frame;
    }

    private async Task<bool> FillAsync(Memory<byte> buffer, CancellationToken cancellation)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer[read..], cancellation);
            if (n == 0)
                return read == 0 ? false : throw new EndOfStreamException();
            read += n;
        }
        return true;
    }
}
