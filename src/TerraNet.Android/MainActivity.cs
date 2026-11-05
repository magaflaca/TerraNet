using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using Android.Views;
using Avalonia.Android;

namespace TerraNet.Android;

[Activity(
    Label = "TerraNet",
    Theme = "@style/TerraNet.Theme",
    Icon = "@mipmap/ic_launcher",
    MainLauncher = true,
    WindowSoftInputMode = SoftInput.AdjustResize,
    LaunchMode = LaunchMode.SingleTask,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize)]
public sealed class MainActivity : AvaloniaMainActivity
{
    private const int NotificationRequest = 7;

    public static MainActivity? Current { get; private set; }

    protected override void OnResume()
    {
        base.OnResume();
        Current = this;
    }

    protected override void OnPause()
    {
        if (Current == this)
            Current = null;
        base.OnPause();
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        if (OperatingSystem.IsAndroidVersionAtLeast(33) &&
            CheckSelfPermission(Manifest.Permission.PostNotifications) != Permission.Granted)
            RequestPermissions([Manifest.Permission.PostNotifications], NotificationRequest);
    }

    public void RequestBackgroundExemption()
    {
        var power = (PowerManager?)GetSystemService(PowerService);
        if (power is null || power.IsIgnoringBatteryOptimizations(PackageName))
            return;
        var intent = new Intent(Settings.ActionRequestIgnoreBatteryOptimizations, global::Android.Net.Uri.Parse("package:" + PackageName));
        StartActivity(intent);
    }
}
