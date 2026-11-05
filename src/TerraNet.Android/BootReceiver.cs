using Android.App;
using Android.Content;
using TerraNet.App.Services;

namespace TerraNet.Android;

[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter([Intent.ActionBootCompleted, Intent.ActionMyPackageReplaced])]
public sealed class BootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || AppHost.TryCurrent is not { } host)
            return;
        var settings = host.Settings.Current;
        if (settings.StartOnBoot && settings.LastWorldId is not null)
            HostService.Resume(context);
    }
}
