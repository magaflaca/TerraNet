using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Net.Wifi;
using Android.OS;
using TerraNet.App.Services;
using TerraNet.Core.Hosting;
using TerraNet.Core.Localization;
using TerraNet.Core.Session;

namespace TerraNet.Android;

[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeSpecialUse)]
[Property("android.app.PROPERTY_SPECIAL_USE_FGS_SUBTYPE", Value = "Runs a dedicated multiplayer game server on the device so friends can keep playing in the hosted world while the app is in the background")]
public sealed class HostService : Service
{
    private const string Channel = "server";
    private const int NotificationId = 1;
    private const string ActionStop = "win.hacks.terranet.STOP";
    private const string ActionResume = "win.hacks.terranet.RESUME";

    private static bool _running;
    private PowerManager.WakeLock? _wake;
    private WifiManager.WifiLock? _wifi;
    private HostSession? _session;

    public static void Ensure(Context context)
    {
        if (_running)
            return;
        context.StartForegroundService(new Intent(context, typeof(HostService)));
    }

    public static void Resume(Context context) =>
        context.StartForegroundService(new Intent(context, typeof(HostService)).SetAction(ActionResume));

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        CreateChannel();
        if (OperatingSystem.IsAndroidVersionAtLeast(34))
            StartForeground(NotificationId, Build(SessionSnapshot.Idle), ForegroundService.TypeSpecialUse);
        else
            StartForeground(NotificationId, Build(SessionSnapshot.Idle));
        _running = true;

        if (intent?.Action == ActionStop)
        {
            _ = StopSessionAsync();
            return StartCommandResult.NotSticky;
        }

        _ = AttachAsync(intent?.Action == ActionResume || intent is null);
        return StartCommandResult.Sticky;
    }

    private async Task AttachAsync(bool resume)
    {
        var host = AppHost.Current;
        try
        {
            _session = await host.SessionAsync();
        }
        catch (Exception)
        {
            StopSelf();
            return;
        }

        _session.Changed -= OnChanged;
        _session.Changed += OnChanged;
        if (resume && !_session.Snapshot.IsActive && host.Settings.Current.LastWorldId is { } world && host.Worlds.Find(world) is not null)
            _session.Start(world);
        OnChanged(_session.Snapshot);
    }

    private async Task StopSessionAsync()
    {
        if (_session is not null)
            await _session.StopAsync();
        else
            StopSelf();
    }

    private void OnChanged(SessionSnapshot snapshot)
    {
        if (!snapshot.IsActive && snapshot.Server is not ServerState.Crashed)
        {
            ReleaseLocks();
            StopForeground(StopForegroundFlags.Remove);
            StopSelf();
            return;
        }
        HoldLocks(AppHost.Current.Settings.Current.KeepAwake);
        var manager = (NotificationManager?)GetSystemService(NotificationService);
        manager?.Notify(NotificationId, Build(snapshot));
    }

    private Notification Build(SessionSnapshot snapshot)
    {
        var world = snapshot.World?.Profile.Name ?? "TerraNet";
        var (title, text) = snapshot.Server switch
        {
            ServerState.Running running => (Loc.Format("notificationOnline", world),
                Loc.Format("notificationPlayers", snapshot.PublicAddress ?? Loc.Get("connecting"), running.Players.Count, running.MaxPlayers)),
            ServerState.Generating g => (world, $"{Loc.Get("generating")} {g.Progress:P0}"),
            ServerState.Stopping => (world, Loc.Get("stopping")),
            ServerState.Crashed => (world, Loc.Get("crashed")),
            _ => (world, Loc.Get("starting"))
        };

        var open = PendingIntent.GetActivity(this, 0, new Intent(this, typeof(MainActivity)).AddFlags(ActivityFlags.SingleTop), PendingIntentFlags.Immutable);
        var stop = PendingIntent.GetService(this, 1, new Intent(this, typeof(HostService)).SetAction(ActionStop), PendingIntentFlags.Immutable);

        return new Notification.Builder(this, Channel)
            .SetSmallIcon(Resource.Mipmap.ic_launcher_monochrome)!
            .SetContentTitle(title)!
            .SetContentText(text)!
            .SetContentIntent(open)!
            .SetOngoing(true)!
            .SetOnlyAlertOnce(true)!
            .SetColor(unchecked((int)0xFF5FC970))!
            .AddAction(new Notification.Action.Builder(null, Loc.Get("stop"), stop).Build())!
            .Build()!;
    }

    private void CreateChannel()
    {
        var manager = (NotificationManager?)GetSystemService(NotificationService);
        manager?.CreateNotificationChannel(new NotificationChannel(Channel, Loc.Get("notificationChannel"), NotificationImportance.Low));
    }

    private void HoldLocks(bool keepAwake)
    {
        if (!keepAwake)
        {
            ReleaseLocks();
            return;
        }
        if (_wake is null && GetSystemService(PowerService) is PowerManager power)
        {
            _wake = power.NewWakeLock(WakeLockFlags.Partial, "terranet:server");
            _wake?.Acquire();
        }
        if (_wifi is null && GetSystemService(WifiService) is WifiManager wifi)
        {
            _wifi = wifi.CreateWifiLock(global::Android.Net.WifiMode.FullLowLatency, "terranet:server");
            _wifi?.Acquire();
        }
    }

    private void ReleaseLocks()
    {
        if (_wake?.IsHeld == true)
            _wake.Release();
        if (_wifi?.IsHeld == true)
            _wifi.Release();
        _wake = null;
        _wifi = null;
    }

    public override void OnDestroy()
    {
        _running = false;
        if (_session is not null)
            _session.Changed -= OnChanged;
        ReleaseLocks();
        base.OnDestroy();
    }
}
