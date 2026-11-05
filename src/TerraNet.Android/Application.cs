using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using TerraNet.App.Services;

namespace TerraNet.Android;

[Application(Label = "TerraNet", Icon = "@mipmap/ic_launcher", RoundIcon = "@mipmap/ic_launcher")]
public sealed class MainApplication(nint handle, JniHandleOwnership ownership) : AvaloniaAndroidApplication<TerraNet.App.App>(handle, ownership)
{
    public override void OnCreate()
    {
        TerraNet.Core.Diagnostics.AppLog.Path = Path.Combine(GetExternalFilesDir(null)!.AbsolutePath, "terranet.log");
        var platform = new AndroidPlatform(this);
        AppHost.Initialize(platform);
        TerraNet.App.App.PlatformFactory = () => platform;
        base.OnCreate();
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) => base.CustomizeAppBuilder(builder);
}
