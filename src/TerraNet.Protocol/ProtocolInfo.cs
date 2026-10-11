namespace TerraNet.Protocol;

public static class ProtocolInfo
{
    public const byte Version = 1;
    public static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(45);
    public static readonly TimeSpan AttachTimeout = TimeSpan.FromSeconds(10);
}
