using System.Text;

namespace TerraNet.Core.Proxy;

public sealed class LegacyTranslator
{
    private const byte Hello = 1;
    private const byte ItemAnimation = 41;
    private const byte ItemTweaker = 88;
    private const byte EmoteBubble = 91;
    private const byte PlayerHurt = 117;
    private const byte NoAnchor = 3;
    private const byte RemoveBubble = 255;

    private readonly short[] _animationMax = new short[256];

    public static bool IsModernHello(ReadOnlySpan<byte> frame) =>
        frame.Length == 3 + 1 + RemoteServer.ModernVersion.Length
        && frame[2] == Hello
        && frame[3] == RemoteServer.ModernVersion.Length
        && Encoding.ASCII.GetString(frame[4..]) == RemoteServer.ModernVersion;

    public static byte[] LegacyHello() => Packet.Build(Hello, w => w.Write(RemoteServer.LegacyVersion));

    public void FromClient(ReadOnlySpan<byte> frame)
    {
        if (frame.Length == 3 + 9 && frame[2] == ItemAnimation)
            _animationMax[frame[3]] = BitConverter.ToInt16(frame[10..]);
    }

    public byte[] FromServer(byte[] frame)
    {
        var id = frame[2];
        if (id is not (ItemAnimation or ItemTweaker or EmoteBubble or PlayerHurt))
            return frame;
        try
        {
            var body = frame[3..];
            var translated = id switch
            {
                ItemAnimation => Animation(body),
                ItemTweaker => Tweaker(body),
                EmoteBubble => Emote(body),
                _ => Hurt(body)
            };
            if (ReferenceEquals(translated, body))
                return frame;
            var result = new byte[translated.Length + 3];
            BitConverter.TryWriteBytes(result.AsSpan(0, 2), (ushort)result.Length);
            result[2] = id;
            translated.CopyTo(result, 3);
            return result;
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException or ArgumentException)
        {
            return frame;
        }
    }

    private byte[] Animation(byte[] body)
    {
        if (body.Length != 7)
            return body;
        var animation = BitConverter.ToInt16(body, 5);
        var max = Math.Max(_animationMax[body[0]], animation);
        var result = new byte[9];
        body.CopyTo(result, 0);
        BitConverter.TryWriteBytes(result.AsSpan(7), max);
        return result;
    }

    private static byte[] Hurt(byte[] body)
    {
        using var reader = new BinaryReader(new MemoryStream(body), Encoding.UTF8);
        reader.ReadByte();
        var reason = reader.ReadByte();
        if ((reason & 1 << 0) != 0) reader.ReadInt16();
        if ((reason & 1 << 1) != 0) reader.ReadInt16();
        if ((reason & 1 << 2) != 0) reader.ReadInt16();
        if ((reason & 1 << 3) != 0) reader.ReadByte();
        if ((reason & 1 << 4) != 0) reader.ReadInt16();
        if ((reason & 1 << 5) != 0) reader.ReadInt16();
        if ((reason & 1 << 6) != 0) reader.ReadByte();
        if ((reason & 1 << 7) != 0) reader.ReadString();
        reader.ReadInt16();
        reader.ReadByte();
        var flags = (int)reader.BaseStream.Position;
        if (flags >= body.Length)
            return body;
        var result = (byte[])body.Clone();
        result[flags] |= 1 << 2;
        return result;
    }

    private static byte[] Tweaker(byte[] body)
    {
        using var reader = new BinaryReader(new MemoryStream(body));
        using var output = new MemoryStream(body.Length);
        using var writer = new BinaryWriter(output);
        writer.Write(reader.ReadInt16());
        var flags = reader.ReadByte();
        writer.Write(flags);
        if ((flags & 1 << 0) != 0) writer.Write(reader.ReadUInt32());
        if ((flags & 1 << 1) != 0) writer.Write(reader.ReadUInt16());
        if ((flags & 1 << 2) != 0) writer.Write(reader.ReadSingle());
        if ((flags & 1 << 3) != 0) writer.Write(reader.ReadUInt16());
        if ((flags & 1 << 4) != 0) writer.Write(reader.ReadUInt16());
        if ((flags & 1 << 5) != 0) writer.Write(reader.ReadInt16());
        if ((flags & 1 << 6) != 0) writer.Write(reader.ReadSingle());
        if ((flags & 1 << 7) != 0)
        {
            var more = reader.ReadByte();
            if ((more & 1 << 0) != 0) reader.ReadUInt16();
            if ((more & 1 << 1) != 0) reader.ReadUInt16();
            writer.Write((byte)(more & ~0b11));
            if ((more & 1 << 2) != 0) writer.Write(reader.ReadSingle());
            if ((more & 1 << 3) != 0) writer.Write(reader.ReadInt16());
            if ((more & 1 << 4) != 0) writer.Write(reader.ReadInt16());
            if ((more & 1 << 5) != 0) writer.Write(reader.ReadBoolean());
        }
        writer.Flush();
        return output.ToArray();
    }

    private static byte[] Emote(byte[] body)
    {
        using var reader = new BinaryReader(new MemoryStream(body));
        using var output = new MemoryStream(body.Length + 4);
        using var writer = new BinaryWriter(output);
        writer.Write(reader.ReadInt32());
        var anchor = reader.ReadByte();
        if (anchor == RemoveBubble)
        {
            writer.Write(anchor);
            writer.Flush();
            return output.ToArray();
        }
        int meta = reader.ReadUInt16();
        var lifeTime = reader.ReadUInt16();
        var emote = reader.ReadByte();
        switch (anchor)
        {
            case 0:
                writer.Write((byte)0);
                writer.Write((ushort)meta);
                break;
            case 1:
                writer.Write((byte)1);
                writer.Write((byte)meta);
                break;
            default:
                writer.Write(NoAnchor);
                break;
        }
        writer.Write(lifeTime);
        writer.Write(emote);
        writer.Flush();
        return output.ToArray();
    }
}
