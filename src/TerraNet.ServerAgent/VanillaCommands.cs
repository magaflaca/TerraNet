using TShockAPI;

namespace TerraNet.ServerAgent;

public static class VanillaCommands
{
    private const string HostOnly = "terranet.host";

    private static readonly HashSet<string> Vanilla = new(StringComparer.OrdinalIgnoreCase)
    {
        "help", "me", "p", "party", "roll", "playing", "who", "online", "death", "pvpdeath", "alldeath", "allpvpdeath"
    };

    public static void Apply()
    {
        foreach (var command in Commands.ChatCommands.Where(c => !c.Names.Any(Vanilla.Contains)))
        {
            command.Permissions.Clear();
            command.Permissions.Add(HostOnly);
        }
    }
}
