using System.Net.Http.Json;
using System.Security.Cryptography;
using TerraNet.Core.Localization;

namespace TerraNet.Core.Updates;

public sealed record ReleaseInfo(
    string Version,
    Dictionary<string, string[]> Notes,
    string Url,
    long Size,
    string Sha256)
{
    public Version ParsedVersion => System.Version.TryParse(Version, out var v) ? v : new Version(0, 0);

    public IReadOnlyList<string> LocalizedNotes =>
        Notes.TryGetValue(Loc.Language, out var notes) || Notes.TryGetValue("en", out notes) ? notes : [];
}

public sealed class UpdateChecker(string platform, Version current)
{
    public const string Endpoint = "https://terra.hacks.win/api/latest";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public Version Current => current;

    public async Task<ReleaseInfo?> CheckAsync(CancellationToken cancellation = default)
    {
        try
        {
            var release = await Http.GetFromJsonAsync<ReleaseInfo>($"{Endpoint}?platform={platform}", JsonDefaults.Options, cancellation);
            return release is not null && release.ParsedVersion > current ? release : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    public static async Task<string> DownloadAsync(ReleaseInfo release, string directory, IProgress<double> progress, CancellationToken cancellation = default)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, Path.GetFileName(new Uri(release.Url).LocalPath));
        using var response = await Http.GetAsync(release.Url, HttpCompletionOption.ResponseHeadersRead, cancellation);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? release.Size;

        await using (var source = await response.Content.ReadAsStreamAsync(cancellation))
        await using (var target = File.Create(path))
        {
            var buffer = new byte[81920];
            long received = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellation)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancellation);
                received += read;
                if (total > 0)
                    progress.Report(received / (double)total);
            }
        }

        await using var check = File.OpenRead(path);
        var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(check, cancellation));
        if (!string.IsNullOrEmpty(release.Sha256) && !hash.Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("checksum mismatch");
        return path;
    }
}
