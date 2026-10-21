using TerraNet.Core.Storage;
using TerraNet.Protocol;

namespace TerraNet.Core.Identity;

public sealed class IdentityStore(AppPaths paths)
{
    private readonly JsonStore<IdentityRecord> _store = new(paths.Identity);

    public DeviceCredentials Load()
    {
        var record = _store.Load();
        if (record is { Device.Length: > 0, Secret.Length: > 0 })
            return new DeviceCredentials(DeviceId.Parse(record.Device), Convert.FromHexString(record.Secret));

        var created = DeviceCredentials.New();
        _store.Save(new IdentityRecord { Device = created.Device.ToString(), Secret = Convert.ToHexStringLower(created.Secret) });
        return created;
    }

    public sealed class IdentityRecord
    {
        public string Device { get; init; } = "";
        public string Secret { get; init; } = "";
    }
}
