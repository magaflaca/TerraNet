namespace TerraNet.Core.Settings;

public enum AppTheme
{
    Auto,
    Light,
    Dark
}

public sealed record HostSettings
{
    public int MaxPlayers { get; init; } = 8;
    public string Password { get; init; } = "";
    public string? LastWorldId { get; init; }
    public bool RestartOnCrash { get; init; } = true;
    public bool StartOnBoot { get; init; }
    public bool KeepAwake { get; init; } = true;
    public bool ResumeAfterRestart { get; init; } = true;
    public int AutoSaveMinutes { get; init; } = 10;
    public AppTheme Theme { get; init; } = AppTheme.Auto;
}
