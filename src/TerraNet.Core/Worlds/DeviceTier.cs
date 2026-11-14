namespace TerraNet.Core.Worlds;

public enum DeviceTier
{
    Low,
    Mid,
    High
}

public static class DeviceTiers
{
    private const long Gigabyte = 1L << 30;

    public static DeviceTier Measure(long memoryBytes, int cores, bool mobile) => mobile switch
    {
        true when memoryBytes < 5 * Gigabyte || cores < 6 => DeviceTier.Low,
        true when memoryBytes < 9 * Gigabyte => DeviceTier.Mid,
        false when memoryBytes < 6 * Gigabyte => DeviceTier.Mid,
        _ => DeviceTier.High
    };

    public static bool IsHeavy(this DeviceTier tier, WorldSize size) => tier switch
    {
        DeviceTier.Low => size >= WorldSize.Medium,
        DeviceTier.Mid => size == WorldSize.Large,
        _ => false
    };
}
