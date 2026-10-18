using TerraNet.Protocol;

namespace TerraNet.Core.Tunnel;

public abstract record TunnelState
{
    public sealed record Offline : TunnelState;

    public sealed record Connecting(int Attempt) : TunnelState;

    public sealed record Online(string Host, int Port) : TunnelState
    {
        public string Address => $"{Host}:{Port}";
    }

    public sealed record Refused(RejectReason Reason) : TunnelState;

    public static readonly TunnelState Idle = new Offline();
}
