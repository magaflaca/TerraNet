using System.Buffers.Binary;
using System.Text;

namespace TerraNet.Protocol;

internal sealed class PayloadWriter
{
    private readonly MemoryStream _buffer = new();

    public void Byte(byte value) => _buffer.WriteByte(value);

    public void Bytes(ReadOnlySpan<byte> value) => _buffer.Write(value);

    public void UInt16(ushort value)
    {
        Span<byte> span = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(span, value);
        _buffer.Write(span);
    }

    public void Int64(long value)
    {
        Span<byte> span = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(span, value);
        _buffer.Write(span);
    }

    public void String(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > byte.MaxValue)
            throw new ProtocolException("string too long");
        Byte((byte)bytes.Length);
        Bytes(bytes);
    }

    public ReadOnlyMemory<byte> ToMemory() => _buffer.GetBuffer().AsMemory(0, (int)_buffer.Length);
}

internal ref struct PayloadReader(ReadOnlySpan<byte> data)
{
    private ReadOnlySpan<byte> _data = data;

    public byte Byte() => Take(1)[0];

    public byte[] Bytes(int length) => Take(length).ToArray();

    public ushort UInt16() => BinaryPrimitives.ReadUInt16BigEndian(Take(2));

    public long Int64() => BinaryPrimitives.ReadInt64BigEndian(Take(8));

    public string String() => Encoding.UTF8.GetString(Take(Byte()));

    private ReadOnlySpan<byte> Take(int length)
    {
        if (_data.Length < length)
            throw new ProtocolException("truncated payload");
        var slice = _data[..length];
        _data = _data[length..];
        return slice;
    }
}

public sealed class ProtocolException(string message) : Exception(message);
