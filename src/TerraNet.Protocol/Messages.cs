namespace TerraNet.Protocol;

public enum MessageType : byte
{
    Hello = 1,
    Welcome = 2,
    Reject = 3,
    Open = 4,
    Attach = 5,
    Ping = 6,
    Pong = 7
}

public enum RejectReason : byte
{
    Unknown = 0,
    UnsupportedVersion = 1,
    InvalidCredentials = 2,
    NoPortsAvailable = 3,
    TooManySessions = 4,
    Banned = 5
}

public abstract record Message
{
    public abstract MessageType Type { get; }
    internal abstract void Write(PayloadWriter writer);
}

public sealed record Hello(byte Version, DeviceId Device, ReadOnlyMemory<byte> Secret, string AppVersion) : Message
{
    public override MessageType Type => MessageType.Hello;

    internal override void Write(PayloadWriter writer)
    {
        writer.Byte(Version);
        writer.Bytes(Device.Value.Span);
        writer.Bytes(Secret.Span);
        writer.String(AppVersion);
    }

    internal static Hello Read(PayloadReader reader) =>
        new(reader.Byte(), new DeviceId(reader.Bytes(DeviceId.Length)), reader.Bytes(DeviceCredentials.SecretLength), reader.String());
}

public sealed record Welcome(ushort Port, string PublicHost) : Message
{
    public override MessageType Type => MessageType.Welcome;

    internal override void Write(PayloadWriter writer)
    {
        writer.UInt16(Port);
        writer.String(PublicHost);
    }

    internal static Welcome Read(PayloadReader reader) => new(reader.UInt16(), reader.String());
}

public sealed record Reject(RejectReason Reason) : Message
{
    public override MessageType Type => MessageType.Reject;

    internal override void Write(PayloadWriter writer) => writer.Byte((byte)Reason);

    internal static Reject Read(PayloadReader reader) => new((RejectReason)reader.Byte());
}

public sealed record Open(StreamToken Token) : Message
{
    public override MessageType Type => MessageType.Open;

    internal override void Write(PayloadWriter writer) => writer.Bytes(Token.Value.Span);

    internal static Open Read(PayloadReader reader) => new(new StreamToken(reader.Bytes(StreamToken.Length)));
}

public sealed record Attach(StreamToken Token) : Message
{
    public override MessageType Type => MessageType.Attach;

    internal override void Write(PayloadWriter writer) => writer.Bytes(Token.Value.Span);

    internal static Attach Read(PayloadReader reader) => new(new StreamToken(reader.Bytes(StreamToken.Length)));
}

public sealed record Ping(long Timestamp) : Message
{
    public override MessageType Type => MessageType.Ping;

    internal override void Write(PayloadWriter writer) => writer.Int64(Timestamp);

    internal static Ping Read(PayloadReader reader) => new(reader.Int64());
}

public sealed record Pong(long Timestamp) : Message
{
    public override MessageType Type => MessageType.Pong;

    internal override void Write(PayloadWriter writer) => writer.Int64(Timestamp);

    internal static Pong Read(PayloadReader reader) => new(reader.Int64());
}
