using System.Globalization;
using System.Text.RegularExpressions;

namespace WinSense.Core.Monitoring;

/// <summary>Turns the per-process "GPU Engine" / "GPU Adapter Memory" counter instances into per-GPU values.</summary>
public static partial class GpuCounters
{
    /// <summary>
    /// GPU usage the way Task Manager computes it: add up every process on each engine
    /// (3D, Copy, Video Decode...), then take the busiest engine of each adapter.
    /// Instance names look like <c>pid_1234_luid_0x00000000_0x000111D0_phys_0_eng_3_engtype_VideoDecode</c>.
    /// </summary>
    public static Dictionary<long, double> UsageByLuid(IEnumerable<(string Instance, double Value)> engineSamples)
    {
        var perEngine = new Dictionary<(long Luid, string Phys, string Eng), double>();
        foreach (var (instance, value) in engineSamples)
        {
            var match = EngineInstance().Match(instance);
            if (!match.Success)
                continue;

            var key = (ParseLuid(match.Groups["high"].Value, match.Groups["low"].Value),
                       match.Groups["phys"].Value, match.Groups["eng"].Value);
            perEngine[key] = perEngine.GetValueOrDefault(key) + value;
        }

        return perEngine
            .GroupBy(e => e.Key.Luid)
            .ToDictionary(g => g.Key, g => Math.Min(100, g.Max(e => e.Value)));
    }

    /// <summary>Instance names look like <c>luid_0x00000000_0x000111D0_phys_0</c>.</summary>
    public static Dictionary<long, ulong> MemoryByLuid(IEnumerable<(string Instance, double Value)> memorySamples)
    {
        var result = new Dictionary<long, ulong>();
        foreach (var (instance, value) in memorySamples)
        {
            var match = AdapterInstance().Match(instance);
            if (!match.Success)
                continue;

            var luid = ParseLuid(match.Groups["high"].Value, match.Groups["low"].Value);
            result[luid] = result.GetValueOrDefault(luid) + (ulong)Math.Max(0, value);
        }
        return result;
    }

    private static long ParseLuid(string high, string low) =>
        ((long)uint.Parse(high, NumberStyles.HexNumber) << 32) | uint.Parse(low, NumberStyles.HexNumber);

    [GeneratedRegex(@"luid_0x(?<high>[0-9a-f]{8})_0x(?<low>[0-9a-f]{8})_phys_(?<phys>\d+)_eng_(?<eng>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex EngineInstance();

    [GeneratedRegex(@"^luid_0x(?<high>[0-9a-f]{8})_0x(?<low>[0-9a-f]{8})_phys_\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex AdapterInstance();
}
