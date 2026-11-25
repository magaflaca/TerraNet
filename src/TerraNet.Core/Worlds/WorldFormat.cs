using System.Text;
using System.Text.Json;

namespace TerraNet.Core.Worlds;

public static class WorldFormat
{
    public const int ServerRelease = 326;
    private const int LatestKnownRelease = 329;
    private const int NoSpidersRelease = 328;
    private const int PointerTableOffset = 24;
    private const int ManifestSearchWindow = 1 << 20;
    private const int TrailingFlags = 3;

    public static int ReadRelease(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> head = stackalloc byte[4];
        return stream.Read(head) == 4 ? BitConverter.ToInt32(head) : 0;
    }

    public static bool TryMakeCompatible(string path)
    {
        try
        {
            var release = ReadRelease(path);
            if (release <= ServerRelease)
                return true;
            if (release > LatestKnownRelease)
                return false;
            var data = File.ReadAllBytes(path);
            if (Downgrade(data, release) is not { } converted)
                return false;
            var temporary = path + ".converting";
            File.WriteAllBytes(temporary, converted);
            File.Move(temporary, path, true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or IndexOutOfRangeException)
        {
            return false;
        }
    }

    private static byte[]? Downgrade(byte[] data, int release)
    {
        var count = BitConverter.ToInt16(data, PointerTableOffset);
        if (count < 2)
            return null;
        var pointers = Enumerable.Range(0, count).Select(i => BitConverter.ToInt32(data, PointerTableOffset + 2 + i * 4)).ToArray();

        var removed = -1;
        if (release >= NoSpidersRelease)
        {
            var manifest = FindManifest(data, pointers[0], pointers[1]);
            if (manifest < 0)
                return null;
            removed = manifest - 1;
        }

        var result = removed < 0 ? (byte[])data.Clone() : [.. data.AsSpan(0, removed), .. data.AsSpan(removed + 1)];
        BitConverter.TryWriteBytes(result.AsSpan(0, 4), ServerRelease);
        if (removed >= 0)
            for (var i = 1; i < count; i++)
                BitConverter.TryWriteBytes(result.AsSpan(PointerTableOffset + 2 + i * 4), pointers[i] - 1);
        return result;
    }

    private static int FindManifest(byte[] data, int headerStart, int headerEnd)
    {
        var lowest = Math.Max(headerStart + TrailingFlags, headerEnd - ManifestSearchWindow);
        for (var start = lowest; start < headerEnd; start++)
        {
            if (!TryReadLength(data, start, headerEnd, out var length, out var prefix))
                continue;
            var text = start + prefix;
            if (text + length != headerEnd)
                continue;
            if (length > 0 && data[text] != (byte)'{')
                continue;
            if (!Flags(data, start - TrailingFlags, TrailingFlags))
                continue;
            if (length > 0 && !IsJson(data, text, length))
                continue;
            return start;
        }
        return -1;
    }

    private static bool Flags(byte[] data, int offset, int count)
    {
        for (var i = offset; i < offset + count; i++)
            if (data[i] > 1)
                return false;
        return true;
    }

    private static bool TryReadLength(byte[] data, int offset, int limit, out int value, out int size)
    {
        value = 0;
        size = 0;
        for (var shift = 0; shift < 35; shift += 7)
        {
            if (offset + size >= limit)
                return false;
            var b = data[offset + size++];
            value |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                return value >= 0;
        }
        return false;
    }

    private static bool IsJson(byte[] data, int offset, int length)
    {
        try
        {
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(data, offset, length));
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
