using TerraNet.Core.Storage;

namespace TerraNet.Core.Settings;

public sealed class SettingsService(AppPaths paths)
{
    private readonly JsonStore<HostSettings> _store = new(paths.Settings);
    private HostSettings? _current;

    public event Action<HostSettings>? Changed;

    public HostSettings Current => _current ??= _store.Load();

    public void Update(Func<HostSettings, HostSettings> change)
    {
        _current = change(Current);
        _store.Save(_current);
        Changed?.Invoke(_current);
    }
}
