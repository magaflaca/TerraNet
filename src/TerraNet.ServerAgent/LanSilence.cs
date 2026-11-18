using System.Net.Sockets;
using System.Reflection;
using Terraria;

namespace TerraNet.ServerAgent;

public static class LanSilence
{
    private static readonly FieldInfo? Client = typeof(Netplay).GetField("BroadcastClient", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

    public static void Start() => Task.Run(async () =>
    {
        while (Client is not null)
        {
            if (Client.GetValue(null) is UdpClient udp)
            {
                udp.Close();
                return;
            }
            await Task.Delay(250);
        }
    });
}
