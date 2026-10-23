using System.Collections.Concurrent;
using System.Text;
using TShockAPI;

namespace TerraNet.ServerAgent;

public static class ConsoleBridge
{
    private static readonly BlockingCollection<string> Pending = new();
    private static readonly TimeSpan ReadyPoll = TimeSpan.FromMilliseconds(250);
    private const string Switch = "TerraNet.ConsoleBridge";
    private static int _started;

    public static void SuppressVanillaInput()
    {
        AppContext.SetSwitch(Switch, true);
        HookEvents.Terraria.Main.startDedInput += (_, args) => args.ContinueExecution = false;
    }

    public static void StartIfSuppressed()
    {
        if (!AppContext.TryGetSwitch(Switch, out var enabled) || !enabled)
            return;
        if (Interlocked.Exchange(ref _started, 1) == 1)
            return;
        Start("TerraNet Console Reader", ReadLoop);
        Start("TerraNet Console Executor", ExecuteLoop);
    }

    private static void Start(string name, ThreadStart body) =>
        new Thread(body) { IsBackground = true, Name = name }.Start();

    private static void ReadLoop()
    {
        using var input = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
        while (true)
        {
            var line = input.ReadLine();
            if (line is null)
            {
                Thread.Sleep(250);
                continue;
            }
            if (!string.IsNullOrWhiteSpace(line))
                Pending.Add(line.Trim());
        }
    }

    private static void ExecuteLoop()
    {
        while (Commands.ChatCommands.Count == 0)
            Thread.Sleep(ReadyPoll);
        foreach (var line in Pending.GetConsumingEnumerable())
        {
            var text = line.StartsWith(Commands.Specifier, StringComparison.Ordinal) || line.StartsWith(Commands.SilentSpecifier, StringComparison.Ordinal)
                ? line
                : Commands.Specifier + line;
            try
            {
                Commands.HandleCommand(TSPlayer.Server, text);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
