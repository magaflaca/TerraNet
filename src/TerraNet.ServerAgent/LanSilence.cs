using System.Net.Sockets;
using System.Reflection;
using Terraria;

namespace TerraNet.ServerAgent;

public static class LanSilence
{
    private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly FieldInfo? Client = typeof(Netplay).GetField("BroadcastClient", AnyStatic);
    private static readonly FieldInfo? Broadcaster = typeof(Netplay).GetField("broadcastThread", AnyStatic);

    public static void Start() => Task.Run(async () =>
    {
        while (Client is not null)
        {
            if (Client.GetValue(null) is UdpClient udp)
            {
                if (Broadcaster?.GetValue(null) is Thread thread)
                    thread.IsBackground = true;
                udp.Close();
                return;
            }
            await Task.Delay(250);
        }
    });
}
