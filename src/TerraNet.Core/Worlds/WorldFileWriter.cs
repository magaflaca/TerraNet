using System.Text;

namespace TerraNet.Core.Worlds;

public static class WorldFileWriter
{
    private const int PointerTableOffset = 24;

    public static bool TryRename(string path, string name)
    {
        try
        {
            var data = File.ReadAllBytes(path);
            var renamed = Rename(data, name);
            var temporary = path + ".renaming";
            File.WriteAllBytes(temporary, renamed);
            File.Move(temporary, path, true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or ArgumentException or IndexOutOfRangeException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static byte[] Rename(byte[] data, string name)
    {
        var count = BitConverter.ToInt16(data, PointerTableOffset);
        var pointers = Enumerable.Range(0, count).Select(i => BitConverter.ToInt32(data, PointerTableOffset + 2 + i * 4)).ToArray();
        var headerStart = pointers[0];
        var footerName = pointers[^1] + 1;
        var encoded = Encode(name);

        var headerLength = EncodedLength(data, headerStart);
        var footerLength = EncodedLength(data, footerName);
        var delta = encoded.Length - headerLength;

        using var output = new MemoryStream(data.Length + delta * 2);
        output.Write(data, 0, headerStart);
        output.Write(encoded);
        output.Write(data, headerStart + headerLength, footerName - headerStart - headerLength);
        output.Write(encoded);
        output.Write(data, footerName + footerLength, data.Length - footerName - footerLength);
        var result = output.ToArray();

        for (var i = 1; i < count; i++)
            BitConverter.TryWriteBytes(result.AsSpan(PointerTableOffset + 2 + i * 4), pointers[i] + delta);
        return result;
    }

    private static byte[] Encode(string value)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            writer.Write(value);
        return stream.ToArray();
    }

    private static int EncodedLength(byte[] data, int offset)
    {
        using var reader = new BinaryReader(new MemoryStream(data, offset, data.Length - offset), Encoding.UTF8);
        var start = reader.BaseStream.Position;
        reader.ReadString();
        return (int)(reader.BaseStream.Position - start);
    }
}
