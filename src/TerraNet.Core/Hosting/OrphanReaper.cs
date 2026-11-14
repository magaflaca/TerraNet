using System.Diagnostics;

namespace TerraNet.Core.Hosting;

public static class OrphanReaper
{
    private static readonly string[] ServerNames = ["TShock.Server", "libtnhost.so", "libtnhost", "TerraNet"];

    public static void Reap(string pidFile)
    {
        if (!File.Exists(pidFile) || !int.TryParse(File.ReadAllText(pidFile).Trim(), out var pid))
            return;
        try
        {
            using var process = Process.GetProcessById(pid);
            if (ServerNames.Any(n => process.ProcessName.StartsWith(n, StringComparison.OrdinalIgnoreCase)) && process.Id != Environment.ProcessId)
            {
                process.Kill(true);
                process.WaitForExit(5000);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
        File.Delete(pidFile);
    }
}
