using System.Buffers.Binary;

namespace TerraNet.Protocol;

public sealed class FrameChannel(Stream stream) : IAsyncDisposable
{
    private const int HeaderLength = 3;
    private const int MaxPayload = 1024;

    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public Stream Stream => stream;

    public async Task SendAsync(Message message, CancellationToken cancellation = default)
    {
        var writer = new PayloadWriter();
        message.Write(writer);
        var payload = writer.ToMemory();

        var frame = new byte[HeaderLength + payload.Length];
        frame[0] = (byte)message.Type;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(1), (ushort)payload.Length);
        payload.CopyTo(frame.AsMemory(HeaderLength));

        await _writeLock.WaitAsync(cancellation);
        try
        {
            await stream.WriteAsync(frame, cancellation);
            await stream.FlushAsync(cancellation);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<Message> ReceiveAsync(CancellationToken cancellation = default)
    {
        var header = new byte[HeaderLength];
        await stream.ReadExactlyAsync(header, cancellation);
        var length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(1));
        if (length > MaxPayload)
            throw new ProtocolException("frame too large");

        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellation);
        return Decode((MessageType)header[0], payload);
    }

    private static Message Decode(MessageType type, ReadOnlySpan<byte> payload)
    {
        var reader = new PayloadReader(payload);
        return type switch
        {
            MessageType.Hello => Hello.Read(reader),
            MessageType.Welcome => Welcome.Read(reader),
            MessageType.Reject => Reject.Read(reader),
            MessageType.Open => Open.Read(reader),
            MessageType.Attach => Attach.Read(reader),
            MessageType.Ping => Ping.Read(reader),
            MessageType.Pong => Pong.Read(reader),
            _ => throw new ProtocolException($"unknown message {type}")
        };
    }

    public async ValueTask DisposeAsync()
    {
        await stream.DisposeAsync();
        _writeLock.Dispose();
    }
}
