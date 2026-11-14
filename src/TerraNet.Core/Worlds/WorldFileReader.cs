using System.Text;

namespace TerraNet.Core.Worlds;

public sealed record WorldFileInfo(string Name, WorldSize Size, WorldDifficulty Difficulty);

public static class WorldFileReader
{
    private const int MinimumVersion = 135;
    private const int GameModeVersion = 209;

    public static WorldFileInfo? TryRead(Stream stream)
    {
        try
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var version = reader.ReadInt32();
            if (version < MinimumVersion)
                return null;
            reader.ReadUInt64();
            reader.ReadUInt32();
            reader.ReadUInt64();
            var positions = new int[reader.ReadInt16()];
            for (var i = 0; i < positions.Length; i++)
                positions[i] = reader.ReadInt32();

            stream.Position = positions[0];
            var name = reader.ReadString();
            if (version >= 179)
            {
                reader.ReadString();
                reader.ReadUInt64();
            }
            if (version >= 181)
                reader.ReadBytes(16);
            reader.ReadInt32();
            reader.ReadInt32();
            reader.ReadInt32();
            reader.ReadInt32();
            reader.ReadInt32();
            reader.ReadInt32();
            var width = reader.ReadInt32();
            var mode = version >= GameModeVersion ? reader.ReadInt32() : reader.ReadBoolean() ? 1 : 0;

            var size = width switch
            {
                <= 4200 => WorldSize.Small,
                <= 6400 => WorldSize.Medium,
                _ => WorldSize.Large
            };
            var difficulty = Enum.IsDefined(typeof(WorldDifficulty), mode) ? (WorldDifficulty)mode : WorldDifficulty.Classic;
            return new WorldFileInfo(name, size, difficulty);
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or ArgumentException)
        {
            return null;
        }
    }
}
