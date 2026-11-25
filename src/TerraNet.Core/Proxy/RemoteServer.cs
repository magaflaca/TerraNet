using System.Net.Sockets;
using System.Text;

namespace TerraNet.Core.Proxy;

public sealed record RemoteWorld(
    string Name,
    ushort Width,
    bool Crimson,
    int GameMode,
    bool HardMode,
    byte MaxPlayers,
    byte Players,
    bool PlayersKnown,
    bool Modern,
    bool Protected);

public enum ProbeError
{
    None,
    Unreachable,
    Rejected,
    NoAnswer
}

public sealed record ProbeResult(RemoteWorld? World, ProbeError Error)
{
    public bool Success => World is not null;
}

public static class RemoteServer
{
    public const string ModernVersion = "Terraria329";
    public const string LegacyVersion = "Terraria326";
    public const byte DefaultMaxPlayers = 16;

    private const byte Hello = 1;
    private const byte Kick = 2;
    private const byte Slot = 3;
    private const byte RequestWorld = 6;
    private const byte ClientUuid = 68;
    private const byte WorldData = 7;
    private const byte PasswordRequest = 37;
    private const byte ServerInfo = 163;
    private const int TreeTopAreas = 13;

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan GreetingWait = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan AnswerTimeout = TimeSpan.FromSeconds(8);

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
            return new ProbeResult(null, ProbeError.Unreachable);
        }

        var stream = client.GetStream();
        var reader = new PacketReader(stream);
        try
        {
            if (await WaitForDataAsync(client, GreetingWait, cancellation))
            {
                var first = await reader.ReadAsync(cancellation);
                if (first is { Id: ServerInfo } info && ParseServerInfo(info.Body) is { } modern)
                    return new ProbeResult(modern, ProbeError.None);
            }
            return await HandshakeAsync(stream, reader, host, cancellation);
        }
        catch (Exception ex) when (ex is IOException or SocketException or EndOfStreamException or OperationCanceledException && !cancellation.IsCancellationRequested)
        {
            return new ProbeResult(null, ProbeError.NoAnswer);
        }
    }

    private static async Task<bool> WaitForDataAsync(TcpClient client, TimeSpan wait, CancellationToken cancellation)
    {
        var deadline = DateTime.UtcNow + wait;
        while (client.Available == 0)
        {
            if (DateTime.UtcNow >= deadline)
                return false;
            await Task.Delay(50, cancellation);
        }
        return true;
    }

    private static async Task<ProbeResult> HandshakeAsync(NetworkStream stream, PacketReader reader, string host, CancellationToken cancellation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(AnswerTimeout);
        await stream.WriteAsync(Packet.Build(Hello, w => w.Write(LegacyVersion)), timeout.Token);
        while (true)
        {
            var packet = await reader.ReadAsync(timeout.Token) ?? throw new EndOfStreamException();
            switch (packet.Id)
            {
                case Kick:
                    return new ProbeResult(null, ProbeError.Rejected);
                case PasswordRequest:
                    return new ProbeResult(new RemoteWorld(host, 4200, false, 0, false, DefaultMaxPlayers, 0, false, false, true), ProbeError.None);
                case Slot:
                    await stream.WriteAsync(Packet.Build(ClientUuid, w => w.Write(Guid.NewGuid().ToString())), timeout.Token);
                    await stream.WriteAsync(Packet.Build(RequestWorld, _ => { }), timeout.Token);
                    break;
                case WorldData when ParseWorldData(packet.Body) is { } world:
                    return new ProbeResult(world, ProbeError.None);
            }
        }
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
            return new RemoteWorld(name, width, crimson, mode, false, max, players, true, true, false);
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException or ArgumentException)
        {
            return null;
        }
    }

    public static RemoteWorld? ParseWorldData(byte[] body)
    {
        try
        {
            using var reader = new BinaryReader(new MemoryStream(body), Encoding.UTF8);
            reader.ReadInt32();
            reader.ReadByte();
            reader.ReadByte();
            var width = reader.ReadInt16();
            reader.ReadBytes(5 * 2 + 4);
            var name = reader.ReadString();
            var mode = reader.ReadByte();
            reader.ReadBytes(16 + 8 + 1 + 13 + 3 + 4 + 1 + 3 * 4 + 4 + 3 * 4 + 4 + TreeTopAreas + 4);
            var first = reader.ReadByte();
            var second = reader.ReadByte();
            return new RemoteWorld(name, (ushort)width, (second & 1 << 5) != 0, mode, (first & 1 << 4) != 0, DefaultMaxPlayers, 0, false, false, false);
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
