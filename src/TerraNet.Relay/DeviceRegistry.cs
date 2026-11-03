using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TerraNet.Protocol;

namespace TerraNet.Relay;

public sealed class DeviceRegistry
{
    private readonly RelayOptions _options;
    private readonly string _path;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, DeviceRecord> _devices;

    public DeviceRegistry(RelayOptions options)
    {
        _options = options;
        _path = Path.Combine(options.DataDirectory, "devices.json");
        Directory.CreateDirectory(options.DataDirectory);
        var stored = File.Exists(_path)
            ? JsonSerializer.Deserialize<Dictionary<string, DeviceRecord>>(File.ReadAllText(_path)) ?? []
            : [];
        _devices = stored
            .Where(d => d.Value.Port >= options.PortRangeStart && d.Value.Port <= options.PortRangeEnd)
            .ToDictionary();
    }

    public RegistrationResult Register(DeviceId device, ReadOnlySpan<byte> secret)
    {
        var key = device.ToString();
        var hash = DeviceCredentials.HashSecret(secret);
        lock (_gate)
        {
            if (_devices.TryGetValue(key, out var existing))
            {
                if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(existing.SecretHash), Encoding.ASCII.GetBytes(hash)))
                    return RegistrationResult.Failed(RejectReason.InvalidCredentials);
                if (existing.Banned)
                    return RegistrationResult.Failed(RejectReason.Banned);
                _devices[key] = existing with { LastSeen = DateTimeOffset.UtcNow };
                Persist();
                return RegistrationResult.Success(existing.Port);
            }

            var port = AllocatePort();
            if (port is null)
                return RegistrationResult.Failed(RejectReason.NoPortsAvailable);

            _devices[key] = new DeviceRecord(hash, port.Value, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false);
            Persist();
            return RegistrationResult.Success(port.Value);
        }
    }

    private int? AllocatePort()
    {
        var used = _devices.Values.Select(d => d.Port).ToHashSet();
        for (var port = _options.PortRangeStart; port <= _options.PortRangeEnd; port++)
            if (!used.Contains(port))
                return port;

        var cutoff = DateTimeOffset.UtcNow.AddDays(-_options.PortRetentionDays);
        var stale = _devices.Where(d => d.Value.LastSeen < cutoff && !d.Value.Banned)
            .OrderBy(d => d.Value.LastSeen)
            .FirstOrDefault();
        if (stale.Key is null)
            return null;

        _devices.Remove(stale.Key);
        return stale.Value.Port;
    }

    private void Persist()
    {
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(_devices, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, _path, true);
    }
}

public sealed record DeviceRecord(string SecretHash, int Port, DateTimeOffset FirstSeen, DateTimeOffset LastSeen, bool Banned);

public readonly record struct RegistrationResult(int Port, RejectReason? Error)
{
    public static RegistrationResult Success(int port) => new(port, null);

    public static RegistrationResult Failed(RejectReason reason) => new(0, reason);
}
