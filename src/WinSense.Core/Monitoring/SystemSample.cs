namespace WinSense.Core.Monitoring;

public sealed record GpuSample(double UsagePercent, ulong? DedicatedMemoryUsedBytes);

/// <summary>One reading of the live values. Null means "not available on this machine".</summary>
public sealed record SystemSample(
    double? CpuUsagePercent,
    double? CpuClockGhz,
    ulong MemoryTotalBytes,
    ulong MemoryUsedBytes,
    IReadOnlyDictionary<long, GpuSample> GpusByLuid,
    TemperatureReading Temperatures);

/// <param name="CpuCelsius">CPU temperature, or null if no sensor.</param>
/// <param name="GpuCelsius">Discrete GPU temperature, or null if no sensor.</param>
/// <param name="SystemCelsius">Generic ACPI thermal zone, used when there is no CPU sensor.</param>
public sealed record TemperatureReading(double? CpuCelsius, double? GpuCelsius, double? SystemCelsius)
{
    public static TemperatureReading None { get; } = new(null, null, null);
}
