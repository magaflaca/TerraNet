namespace TerraNet.Core.Worlds;

public enum WorldSize
{
    Small = 1,
    Medium = 2,
    Large = 3
}

public enum WorldDifficulty
{
    Classic = 0,
    Expert = 1,
    Master = 2,
    Journey = 3
}

public enum WorldEvil
{
    Random,
    Corruption,
    Crimson
}

public sealed record WorldProfile
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..12];
    public string Name { get; init; } = "";
    public WorldSize Size { get; init; } = WorldSize.Small;
    public WorldDifficulty Difficulty { get; init; } = WorldDifficulty.Classic;
    public WorldEvil Evil { get; init; } = WorldEvil.Random;
    public string Seed { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastPlayedAt { get; init; }
}
