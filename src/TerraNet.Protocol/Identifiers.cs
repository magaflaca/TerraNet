using System.Security.Cryptography;

namespace TerraNet.Protocol;

public readonly record struct DeviceId(ReadOnlyMemory<byte> Value)
{
    public const int Length = 16;

    public static DeviceId New() => new(RandomNumberGenerator.GetBytes(Length));

    public static DeviceId Parse(string hex) => new(Convert.FromHexString(hex));

    public override string ToString() => Convert.ToHexStringLower(Value.Span);

    public bool Equals(DeviceId other) => Value.Span.SequenceEqual(other.Value.Span);

    public override int GetHashCode() => BitConverter.ToInt32(Value.Span[..4]);
}

public readonly record struct StreamToken(ReadOnlyMemory<byte> Value)
{
    public const int Length = 16;

    public static StreamToken New() => new(RandomNumberGenerator.GetBytes(Length));

    public override string ToString() => Convert.ToHexStringLower(Value.Span);

    public bool Equals(StreamToken other) => Value.Span.SequenceEqual(other.Value.Span);

    public override int GetHashCode() => BitConverter.ToInt32(Value.Span[..4]);
}

public sealed record DeviceCredentials(DeviceId Device, byte[] Secret)
{
    public const int SecretLength = 32;

    public static DeviceCredentials New() => new(DeviceId.New(), RandomNumberGenerator.GetBytes(SecretLength));

    public static string HashSecret(ReadOnlySpan<byte> secret) => Convert.ToHexStringLower(SHA256.HashData(secret));
}
