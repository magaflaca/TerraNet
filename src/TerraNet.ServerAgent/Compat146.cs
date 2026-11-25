using System.Text;
using Terraria;
using Terraria.DataStructures;
using TerrariaApi.Server;
using TShockAPI;

namespace TerraNet.ServerAgent;

public sealed class Compat146(TerrariaPlugin owner)
{
    private const string NewVersion = "Terraria329";
    private const string ServerVersion = "Terraria326";
    private const string StandaloneName = "Compat146";

    private static readonly bool[] IsNewClient = new bool[256];
    private static readonly short[] LastAnimationMax = new short[256];

    private bool _registered;

    public void Register()
    {
        ServerApi.Hooks.NetGetData.Register(owner, OnGetData, int.MaxValue);
        ServerApi.Hooks.NetSendBytes.Register(owner, OnSendBytes, int.MaxValue);
        ServerApi.Hooks.ServerLeave.Register(owner, OnLeave);
        _registered = true;
    }

    public void YieldToStandalone()
    {
        if (!ServerApi.Plugins.Any(p => p.Plugin.Name == StandaloneName && p.Plugin != owner))
            return;
        Deregister();
        TShock.Log.ConsoleInfo("[TerraNet] Compat146 plugin found, using it for 1.4.6 clients.");
    }

    public void Deregister()
    {
        if (!_registered)
            return;
        ServerApi.Hooks.NetGetData.Deregister(owner, OnGetData);
        ServerApi.Hooks.NetSendBytes.Deregister(owner, OnSendBytes);
        ServerApi.Hooks.ServerLeave.Deregister(owner, OnLeave);
        _registered = false;
    }

    private static void OnLeave(LeaveEventArgs args)
    {
        if (args.Who < 0 || args.Who >= IsNewClient.Length)
            return;
        IsNewClient[args.Who] = false;
        LastAnimationMax[args.Who] = 0;
    }

    private static void OnGetData(GetDataEventArgs args)
    {
        if (args.Handled)
            return;

        var who = args.Msg.whoAmI;
        if (args.MsgID == PacketTypes.PlayerAnimation)
        {
            if (IsNewClient[who] && args.Length - 1 >= 9)
                LastAnimationMax[who] = BitConverter.ToInt16(args.Msg.readBuffer, args.Index + 7);
            return;
        }
        if (args.MsgID != PacketTypes.ConnectRequest)
            return;

        var buffer = args.Msg.readBuffer;
        var start = args.Index;
        if (args.Length - 1 < 1 + NewVersion.Length || buffer[start] != NewVersion.Length)
        {
            IsNewClient[who] = false;
            return;
        }

        if (Encoding.ASCII.GetString(buffer, start + 1, NewVersion.Length) != NewVersion)
        {
            IsNewClient[who] = false;
            return;
        }
        Encoding.ASCII.GetBytes(ServerVersion, 0, ServerVersion.Length, buffer, start + 1);
        IsNewClient[who] = true;
        LastAnimationMax[who] = 0;
        TShock.Log.ConsoleInfo($"[TerraNet] Slot {who} is a Terraria 1.4.6 client.");
    }

    private static void OnSendBytes(SendBytesEventArgs args)
    {
        if (args.Handled || args.Socket is null)
            return;

        var who = args.Socket.Id;
        if (who < 0 || who >= IsNewClient.Length || !IsNewClient[who])
            return;
        if (!NeedsTranslation(args.Buffer, args.Offset, args.Count))
            return;

        byte[] translated;
        try
        {
            translated = Translate(args.Buffer, args.Offset, args.Count);
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[TerraNet] Could not translate a packet for slot {who}: {ex.Message}");
            return;
        }

        args.Handled = true;
        try
        {
            args.Socket.Socket?.AsyncSend(translated, 0, translated.Length, args.Socket.ServerWriteCallBack);
        }
        catch
        {
        }
    }

    private static bool IsTranslated(byte id) => id is 41 or 88 or 91 or 117;

    private static bool NeedsTranslation(byte[] buffer, int offset, int count)
    {
        var position = offset;
        var end = offset + count;
        while (position + 3 <= end)
        {
            int length = BitConverter.ToUInt16(buffer, position);
            if (length < 3)
                return false;
            if (IsTranslated(buffer[position + 2]))
                return true;
            position += length;
        }
        return false;
    }

    private static byte[] Translate(byte[] buffer, int offset, int count)
    {
        using var output = new MemoryStream(count + 16);
        var position = offset;
        var end = offset + count;
        while (position + 3 <= end)
        {
            int length = BitConverter.ToUInt16(buffer, position);
            if (length < 3 || position + length > end)
                break;

            var id = buffer[position + 2];
            var body = buffer.AsSpan(position + 3, length - 3).ToArray();
            var translated = id switch
            {
                41 => ItemAnimation(body),
                88 => ItemTweaker(body),
                91 => EmoteBubble(body),
                117 => PlayerHurt(body),
                _ => body
            };

            var newLength = translated.Length + 3;
            output.WriteByte((byte)(newLength & 0xFF));
            output.WriteByte((byte)(newLength >> 8));
            output.WriteByte(id);
            output.Write(translated);
            position += length;
        }
        if (position < end)
            output.Write(buffer, position, end - position);
        return output.ToArray();
    }

    private static byte[] ItemAnimation(byte[] body)
    {
        if (body.Length != 7)
            return body;
        int player = body[0];
        var animation = BitConverter.ToInt16(body, 5);
        var animationMax = LastAnimationMax[player];
        if (animationMax <= 0 && player < Main.player.Length && Main.player[player] is { } state)
            animationMax = (short)state.itemAnimationMax;
        if (animationMax < animation)
            animationMax = animation;
        var result = new byte[9];
        body.CopyTo(result, 0);
        BitConverter.GetBytes(animationMax).CopyTo(result, 7);
        return result;
    }

    private static byte[] PlayerHurt(byte[] body)
    {
        using var reader = new BinaryReader(new MemoryStream(body));
        reader.ReadByte();
        PlayerDeathReason.FromReader(reader);
        reader.ReadInt16();
        reader.ReadByte();
        var flags = (int)reader.BaseStream.Position;
        if (flags >= body.Length)
            return body;
        var result = (byte[])body.Clone();
        result[flags] |= 1 << 2;
        return result;
    }

    private static byte[] ItemTweaker(byte[] body)
    {
        using var reader = new BinaryReader(new MemoryStream(body));
        using var output = new MemoryStream(body.Length);
        using var writer = new BinaryWriter(output);

        writer.Write(reader.ReadInt16());
        BitsByte flags = reader.ReadByte();
        writer.Write(flags);
        if (flags[0]) writer.Write(reader.ReadUInt32());
        if (flags[1]) writer.Write(reader.ReadUInt16());
        if (flags[2]) writer.Write(reader.ReadSingle());
        if (flags[3]) writer.Write(reader.ReadUInt16());
        if (flags[4]) writer.Write(reader.ReadUInt16());
        if (flags[5]) writer.Write(reader.ReadInt16());
        if (flags[6]) writer.Write(reader.ReadSingle());
        if (flags[7])
        {
            BitsByte more = reader.ReadByte();
            if (more[0]) reader.ReadUInt16();
            if (more[1]) reader.ReadUInt16();
            BitsByte kept = more;
            kept[0] = false;
            kept[1] = false;
            writer.Write(kept);
            if (more[2]) writer.Write(reader.ReadSingle());
            if (more[3]) writer.Write(reader.ReadInt16());
            if (more[4]) writer.Write(reader.ReadInt16());
            if (more[5]) writer.Write(reader.ReadBoolean());
        }
        writer.Flush();
        return output.ToArray();
    }

    private static byte[] EmoteBubble(byte[] body)
    {
        using var reader = new BinaryReader(new MemoryStream(body));
        using var output = new MemoryStream(body.Length + 4);
        using var writer = new BinaryWriter(output);

        writer.Write(reader.ReadInt32());
        var anchor = reader.ReadByte();
        if (anchor == 255)
        {
            writer.Write(anchor);
            writer.Flush();
            return output.ToArray();
        }

        int meta = reader.ReadUInt16();
        var lifeTime = reader.ReadUInt16();
        var emote = reader.ReadByte();
        switch (anchor)
        {
            case 0:
                writer.Write((byte)0);
                writer.Write((ushort)meta);
                break;
            case 1:
                writer.Write((byte)1);
                writer.Write((byte)meta);
                break;
            case 2 when meta < Main.projectile.Length && Main.projectile[meta] is { } projectile:
                writer.Write((byte)2);
                writer.Write((int)projectile.key);
                break;
            default:
                writer.Write((byte)3);
                break;
        }
        writer.Write(lifeTime);
        writer.Write(emote);
        writer.Flush();
        return output.ToArray();
    }
}
