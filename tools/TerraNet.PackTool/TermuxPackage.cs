using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Formats.Tar;
using SharpCompress.Compressors.Xz;

namespace TerraNet.PackTool;

public sealed class TermuxPackage(string cacheRoot)
{
    private const string Repository = "https://packages.termux.dev/apt/termux-main";
    private const string Prefix = "data/data/com.termux/files/usr/";
    private static readonly HttpClient Http = new();

    public async Task<string> ExtractAsync(string package, IReadOnlyList<string> files)
    {
        var (filename, sha256) = await LocateAsync(package);
        var target = Path.Combine(cacheRoot, "termux", Path.GetFileNameWithoutExtension(filename).Replace(':', '_'));
        if (files.All(f => File.Exists(Path.Combine(target, Path.GetFileName(f)))))
            return target;

        Console.WriteLine($"  fetch termux {filename}");
        var deb = await Http.GetByteArrayAsync($"{Repository}/{filename}");
        if (!Convert.ToHexStringLower(SHA256.HashData(deb)).Equals(sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"checksum mismatch for {filename}");

        Directory.CreateDirectory(target);
        await using var data = OpenDataArchive(deb);
        await using var tar = new TarReader(data);
        while (await tar.GetNextEntryAsync() is { } entry)
        {
            var relative = entry.Name.TrimStart('.', '/');
            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile) || !relative.StartsWith(Prefix, StringComparison.Ordinal))
                continue;
            var match = files.FirstOrDefault(f => relative[Prefix.Length..] == f);
            if (match is not null)
                await entry.ExtractToFileAsync(Path.Combine(target, Path.GetFileName(match)), true);
        }
        return target;
    }

    private static async Task<(string Filename, string Sha256)> LocateAsync(string package)
    {
        var index = await Http.GetByteArrayAsync($"{Repository}/dists/stable/main/binary-aarch64/Packages.gz");
        using var gzip = new GZipStream(new MemoryStream(index), CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        var text = await reader.ReadToEndAsync();

        var block = text.Split("\n\n").FirstOrDefault(b => b.StartsWith($"Package: {package}\n", StringComparison.Ordinal))
                    ?? throw new InvalidDataException($"termux package {package} not found");
        string Field(string name) => block.Split('\n').First(l => l.StartsWith(name + ": ", StringComparison.Ordinal))[(name.Length + 2)..].Trim();
        return (Field("Filename"), Field("SHA256"));
    }

    private static Stream OpenDataArchive(byte[] deb)
    {
        var offset = 8;
        while (offset < deb.Length)
        {
            var name = Encoding.ASCII.GetString(deb, offset, 16).Trim();
            var size = int.Parse(Encoding.ASCII.GetString(deb, offset + 48, 10).Trim());
            var body = new MemoryStream(deb, offset + 60, size, false);
            if (name.StartsWith("data.tar.xz", StringComparison.Ordinal))
            {
                var buffer = new MemoryStream();
                using (var xz = new XZStream(body))
                    xz.CopyTo(buffer);
                buffer.Position = 0;
                return buffer;
            }
            offset += 60 + size + (size & 1);
        }
        throw new InvalidDataException("data.tar.xz not found in package");
    }
}
