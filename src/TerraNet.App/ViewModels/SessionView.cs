using TerraNet.App.Controls;
using TerraNet.Core.Hosting;
using TerraNet.Core.Localization;
using TerraNet.Core.Session;
using TerraNet.Core.Tunnel;

namespace TerraNet.App.ViewModels;

public sealed record SessionView(
    bool IsActive,
    bool IsBusy,
    bool IsRunning,
    bool IsCrashed,
    string Status,
    bool ShowProgress,
    bool IsIndeterminate,
    double Progress,
    string WorldName,
    string? Address,
    bool IsConnecting,
    string PlayersText,
    IReadOnlyList<string> Players,
    SceneMood Mood,
    bool IsBridge)
{
    public static readonly SessionView Idle = From(SessionSnapshot.Idle, null);

    public bool HasAddress => Address is not null;
    public string Host => Address?.Split(':')[0] ?? "";
    public string Port => Address?.Split(':').ElementAtOrDefault(1) ?? "";
    public bool HasPlayers => Players.Count > 0;
    public string Icon => IsBridge ? "gamepad" : "world";

    public static SessionView From(SessionSnapshot snapshot, double? preparing)
    {
        var server = snapshot.Server;
        var (status, progress, indeterminate) = server switch
        {
            ServerState.Starting when preparing is not null => (Loc.Get("preparing"), preparing.Value, false),
            ServerState.Starting => (Loc.Get("starting"), 0, true),
            ServerState.Generating g => (Loc.Get("generating"), g.Progress, false),
            ServerState.Running => (Loc.Get("online"), 1, false),
            ServerState.Stopping => (Loc.Get("stopping"), 0, true),
            ServerState.Crashed => (Loc.Get("crashed"), 0, false),
            ServerState.Bridged { Reachable: true } => (Loc.Get("online"), 1, false),
            ServerState.Bridged => (Loc.Get("bridgeOffline"), 0, false),
            _ => ("", 0, false)
        };

        var running = server as ServerState.Running;
        var players = running?.Players ?? [];
        var mood = server switch
        {
            ServerState.Running or ServerState.Bridged { Reachable: true } => SceneMood.Online,
            ServerState.Starting or ServerState.Generating or ServerState.Stopping => SceneMood.Busy,
            _ => SceneMood.Idle
        };

        return new SessionView(
            server.IsActive,
            server is ServerState.Starting or ServerState.Generating or ServerState.Stopping,
            running is not null,
            server is ServerState.Crashed,
            status,
            server is ServerState.Starting or ServerState.Generating or ServerState.Stopping,
            indeterminate,
            progress,
            server is ServerState.Bridged ? Loc.Get("bridgeName") : snapshot.World?.Profile.Name ?? "",
            snapshot.PublicAddress,
            server.IsActive && snapshot.Tunnel is TunnelState.Connecting or TunnelState.Refused,
            running is null ? "" : $"{players.Count}/{running.MaxPlayers}",
            players,
            mood,
            server is ServerState.Bridged);
    }
}
