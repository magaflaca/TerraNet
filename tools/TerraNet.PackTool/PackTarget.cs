namespace TerraNet.PackTool;

public enum HostKind
{
    AppHost,
    NativeLauncher
}

public sealed record PackTarget(
    string Name,
    string RuntimeRid,
    string AssetRid,
    HostKind Host,
    NativeAsset Sqlite,
    IReadOnlyList<string> OpenSslFiles,
    IReadOnlyDictionary<string, string> Environment)
{
    public const string RuntimeDirectoryToken = "{runtime}";
    public const string PackToken = "{pack}";

    public static readonly IReadOnlyList<PackTarget> All =
    [
        new("android-arm64", "linux-bionic-arm64", "linux-arm64", HostKind.NativeLauncher,
            new NativeAsset("sqlitepclraw.lib.e_sqlite3.android", "lib/net6.0-android31.0/SQLitePCLRaw.lib.e_sqlite3.android.aar", "jni/arm64-v8a/libe_sqlite3.so", "libe_sqlite3.so"),
            ["lib/libssl.so.3", "lib/libcrypto.so.3"],
            new Dictionary<string, string>
            {
                ["LD_LIBRARY_PATH"] = RuntimeDirectoryToken,
                ["DOTNET_OPENSSL_VERSION_OVERRIDE"] = "3"
            }),
        new("win-x64", "win-x64", "win-x64", HostKind.AppHost,
            new NativeAsset("sqlitepclraw.lib.e_sqlite3", "runtimes/win-x64/native/e_sqlite3.dll", null, "e_sqlite3.dll"),
            [],
            new Dictionary<string, string>()),
        new("linux-x64", "linux-x64", "linux-x64", HostKind.AppHost,
            new NativeAsset("sqlitepclraw.lib.e_sqlite3", "runtimes/linux-x64/native/libe_sqlite3.so", null, "libe_sqlite3.so"),
            [],
            new Dictionary<string, string>())
    ];

    public string ExecutableSuffix => RuntimeRid.StartsWith("win", StringComparison.Ordinal) ? ".exe" : "";

    public static PackTarget Find(string name) =>
        All.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown target '{name}'. Known: {string.Join(", ", All.Select(t => t.Name))}");
}

public sealed record NativeAsset(string Package, string EntryPath, string? NestedEntryPath, string FileName);
