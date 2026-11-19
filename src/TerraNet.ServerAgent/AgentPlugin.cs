using Terraria;
using TerraNet.Agent;
using TerrariaApi.Server;
using TShockAPI;

namespace TerraNet.ServerAgent;

[ApiVersion(2, 1)]
public sealed class AgentPlugin(Main game) : TerrariaPlugin(game)
{
    public override string Name => "TerraNet";
    public override string Author => "TerraNet";
    public override string Description => "Reports server state to the host app.";
    public override Version Version => typeof(AgentPlugin).Assembly.GetName().Version ?? new Version(1, 0);

    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(500);
    private Timer? _progress;
    private double _lastProgress = -1;

    public override void Initialize()
    {
        _progress = new Timer(_ => ReportProgress(), null, ProgressInterval, ProgressInterval);
        ConsoleBridge.StartIfSuppressed();
        ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInitialize);
        ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreet);
        ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
        ServerApi.Hooks.WorldSave.Register(this, OnSave);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInitialize);
            ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreet);
            ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
            ServerApi.Hooks.WorldSave.Deregister(this, OnSave);
            _progress?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void OnPostInitialize(EventArgs args)
    {
        _progress?.Dispose();
        _progress = null;
        VanillaPermissions.Apply();
        if (Environment.GetEnvironmentVariable(AgentWire.PlayerCommandsVariable) != "1")
            VanillaCommands.Apply();
        LanSilence.Start();
        Emit(new AgentEvent(AgentEventKind.Ready, Main.worldName, Netplay.ListenPort, TShock.Config.Settings.MaxSlots));
    }

    private void ReportProgress()
    {
        if (!WorldGen.generatingWorld)
            return;
        var value = Math.Round(Math.Clamp(Main.AutogenProgress.TotalProgress, 0, 1), 3);
        if (value <= _lastProgress)
            return;
        _lastProgress = value;
        Emit(new AgentEvent(AgentEventKind.Progress, Progress: value));
    }

    private static void OnGreet(GreetPlayerEventArgs args) => EmitPlayers(-1);

    private static void OnLeave(LeaveEventArgs args) => EmitPlayers(args.Who);

    private static void OnSave(WorldSaveEventArgs args) => Emit(new AgentEvent(AgentEventKind.Saved, Main.worldName));

    private static void EmitPlayers(int leaving)
    {
        var names = TShock.Players
            .Where(p => p is { Active: true, RealPlayer: true } && p.Index != leaving)
            .Select(p => p.Name)
            .ToArray();
        Emit(new AgentEvent(AgentEventKind.Players, Players: names));
    }

    private static void Emit(AgentEvent value) => Console.Out.WriteLine(AgentWire.Encode(value));
}
