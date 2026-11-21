namespace TerraNet.Core.Settings;

public enum AppTheme
{
    Auto,
    Light,
    Dark
}

public enum PvpMode
{
    Normal,
    Always,
    Disabled
}

public sealed record HostSettings
{
    public int MaxPlayers { get; init; } = 8;
    public string Password { get; init; } = "";
    public string? LastWorldId { get; init; }
    public bool RestartOnCrash { get; init; } = true;
    public bool StartOnBoot { get; init; }
    public bool KeepInTray { get; init; }
    public bool KeepAwake { get; init; } = true;
    public bool ResumeAfterRestart { get; init; } = true;
    public int AutoSaveMinutes { get; init; } = 10;
    public AppTheme Theme { get; init; } = AppTheme.Auto;
    public bool Sounds { get; init; } = true;
    public string? WelcomeMessage { get; init; }
    public PvpMode Pvp { get; init; } = PvpMode.Normal;
    public bool SpawnProtection { get; init; }
    public int SpawnProtectionRadius { get; init; } = 10;
    public bool AntiCheat { get; init; }
    public bool PlayerCommands { get; init; }
    public IReadOnlyDictionary<string, string> TShockOverrides { get; init; } = new Dictionary<string, string>();
}
