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
    string SharedLibraryPattern,
    HostKind Host,
    NativeAsset Sqlite)
{
    public static readonly IReadOnlyList<PackTarget> All =
    [
        new("android-arm64", "linux-bionic-arm64", "linux-arm64", "*.so", HostKind.NativeLauncher,
            new NativeAsset("sqlitepclraw.lib.e_sqlite3.android", "lib/net6.0-android31.0/SQLitePCLRaw.lib.e_sqlite3.android.aar", "jni/arm64-v8a/libe_sqlite3.so", "libe_sqlite3.so")),
        new("win-x64", "win-x64", "win-x64", "*.dll", HostKind.AppHost,
            new NativeAsset("sqlitepclraw.lib.e_sqlite3", "runtimes/win-x64/native/e_sqlite3.dll", null, "e_sqlite3.dll")),
        new("linux-x64", "linux-x64", "linux-x64", "*.so", HostKind.AppHost,
            new NativeAsset("sqlitepclraw.lib.e_sqlite3", "runtimes/linux-x64/native/libe_sqlite3.so", null, "libe_sqlite3.so"))
    ];

    public static PackTarget Find(string name) =>
        All.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown target '{name}'. Known: {string.Join(", ", All.Select(t => t.Name))}");
}

public sealed record NativeAsset(string Package, string EntryPath, string? NestedEntryPath, string FileName);
