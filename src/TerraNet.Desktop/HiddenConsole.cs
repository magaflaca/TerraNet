using System.Runtime.InteropServices;

namespace TerraNet.Desktop;

internal static partial class HiddenConsole
{
    private const int NoWindowMode = 2;
    private static readonly int[] Streams = [-10, -11, -12];

    public static void Attach()
    {
        var handles = Streams.Select(GetStdHandle).ToArray();
        if (!TryAllocateWithoutWindow())
        {
            AllocConsole();
            ShowWindow(GetConsoleWindow(), 0);
        }
        for (var i = 0; i < Streams.Length; i++)
            if (handles[i] != IntPtr.Zero && handles[i] != -1)
                SetStdHandle(Streams[i], handles[i]);
    }

    private static bool TryAllocateWithoutWindow()
    {
        try
        {
            var options = new AllocOptions { Mode = NoWindowMode };
            return AllocConsoleWithOptions(ref options, out _) >= 0;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AllocOptions
    {
        public int Mode;
        public int UseShowWindow;
        public ushort ShowWindow;
    }

    [LibraryImport("kernel32.dll")]
    private static partial int AllocConsoleWithOptions(ref AllocOptions options, out int result);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllocConsole();

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GetConsoleWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr window, int command);

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GetStdHandle(int stream);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetStdHandle(int stream, IntPtr handle);
}
