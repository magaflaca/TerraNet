using System.Collections.Concurrent;

namespace TerraNet.App.Services;

public enum Sound
{
    Click,
    Toggle,
    Select,
    Pop,
    Copy,
    Online,
    Error,
    Create,
    Stop
}

public static class Sfx
{
    private static readonly ConcurrentDictionary<Sound, byte[]> Cache = new();

    public static void Play(Sound sound)
    {
        if (AppHost.TryCurrent is not { } host || !host.Settings.Current.Sounds)
            return;
        var data = Cache.GetOrAdd(sound, Load);
        if (data.Length > 0)
            host.Platform.PlaySound(sound.ToString().ToLowerInvariant(), data);
    }

    private static byte[] Load(Sound sound)
    {
        using var stream = typeof(Sfx).Assembly.GetManifestResourceStream($"TerraNet.Sounds.{sound.ToString().ToLowerInvariant()}.wav");
        if (stream is null)
            return [];
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
