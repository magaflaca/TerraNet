using System.Diagnostics;
using System.Reflection;
using System.Security;
using System.Text;
using Microsoft.Win32;
using TerraNet.Core.Diagnostics;

namespace TerraNet.Desktop;

public static class WindowsToast
{
    private const string AppId = "TerraNet.Desktop";
    private const string LogoResource = "TerraNet.ToastLogo.png";

    public static void Show(string title, string body)
    {
        try
        {
            Register();
            var xml = $"<toast><visual><binding template=\"ToastGeneric\"><text>{SecurityElement.Escape(title)}</text><text>{SecurityElement.Escape(body)}</text></binding></visual><audio silent=\"true\"/></toast>";
            var script = string.Join("\n",
                "[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] > $null",
                "[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] > $null",
                "$doc = New-Object Windows.Data.Xml.Dom.XmlDocument",
                $"$doc.LoadXml('{xml.Replace("'", "''")}')",
                $"[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('{AppId}').Show([Windows.UI.Notifications.ToastNotification]::new($doc))");
            Process.Start(new ProcessStartInfo("powershell.exe",
                $"-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand {Convert.ToBase64String(Encoding.Unicode.GetBytes(script))}")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            AppLog.Error("toast", ex);
        }
    }

    private static void Register()
    {
        var logo = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TerraNet", "toast.png");
        if (!File.Exists(logo))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logo)!);
            using var source = Assembly.GetExecutingAssembly().GetManifestResourceStream(LogoResource)!;
            using var target = File.Create(logo);
            source.CopyTo(target);
        }
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\AppUserModelId\" + AppId);
        key.SetValue("DisplayName", "TerraNet");
        key.SetValue("IconUri", logo);
    }
}
