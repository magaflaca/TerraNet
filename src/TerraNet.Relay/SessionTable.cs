using System.Net;
using TerraNet.Protocol;

namespace TerraNet.Relay;

public sealed class SessionTable(RelayOptions options)
{
    private readonly Lock _gate = new();
    private readonly Dictionary<DeviceId, TunnelSession> _sessions = [];

    public async Task<bool> TryAddAsync(TunnelSession session)
    {
        TunnelSession? previous;
        lock (_gate)
        {
            _sessions.TryGetValue(session.Device, out previous);
            var fromAddress = _sessions.Values.Count(s => s.Address.Equals(session.Address) && !s.Device.Equals(session.Device));
            if (fromAddress >= options.MaxSessionsPerAddress)
                return false;
            _sessions[session.Device] = session;
        }

        if (previous is not null)
            await previous.DisposeAsync();
        return true;
    }

    public void Remove(TunnelSession session)
    {
        lock (_gate)
        {
            if (_sessions.TryGetValue(session.Device, out var current) && ReferenceEquals(current, session))
                _sessions.Remove(session.Device);
        }
    }

    public IReadOnlySet<int> ActivePorts
    {
        get
        {
            lock (_gate)
                return _sessions.Values.Select(s => s.Port).ToHashSet();
        }
    }

    public IReadOnlyList<DeviceId> ActiveDevices
    {
        get
        {
            lock (_gate)
                return _sessions.Keys.ToList();
        }
    }

    public int Count
    {
        get
        {
            lock (_gate)
                return _sessions.Count;
        }
    }

    public static IPAddress Normalize(IPAddress address) => address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
}
