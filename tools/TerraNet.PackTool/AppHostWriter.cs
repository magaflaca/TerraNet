using System.Text;

namespace TerraNet.PackTool;

public static class AppHostWriter
{
    private static readonly byte[] Placeholder = Encoding.UTF8.GetBytes("c3ab8ff13720e8ad9047dd39466b3c8974e592c2fa383d4a3960714caef0c4f2");

    public static void Write(string template, string destination, string appDllName)
    {
        var bytes = File.ReadAllBytes(template);
        var index = bytes.AsSpan().IndexOf(Placeholder);
        if (index < 0)
            throw new InvalidDataException("apphost placeholder not found");

        var name = Encoding.UTF8.GetBytes(appDllName);
        if (name.Length >= Placeholder.Length)
            throw new ArgumentException("app name too long", nameof(appDllName));

        var slot = bytes.AsSpan(index, Placeholder.Length);
        slot.Clear();
        name.CopyTo(slot);
        File.WriteAllBytes(destination, bytes);
    }
}
