namespace TerraNet.Core.Hosting;

public abstract record ServerState
{
    public sealed record Stopped : ServerState;

    public sealed record Starting : ServerState;

    public sealed record Generating(double Progress) : ServerState;

    public sealed record Running(string World, int MaxPlayers, IReadOnlyList<string> Players) : ServerState;

    public sealed record Stopping : ServerState;

    public sealed record Crashed(int ExitCode) : ServerState;

    public static readonly ServerState Idle = new Stopped();

    public bool IsActive => this is Starting or Generating or Running or Stopping;
}
