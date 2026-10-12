namespace WinSense.Core.Monitoring;

/// <param name="ComputerName">The device name shown in Windows Settings › System › About.</param>
/// <param name="Model">Manufacturer and model, e.g. "Acer Nitro V 15 (Nitro ANV15-41)"; null if the firmware does not say.</param>
public sealed record SystemInfo(string ComputerName, string? Model);

public sealed record CpuInfo(string Name, int Cores, int Threads, int BaseClockMhz);

public sealed record MemoryInfo(ulong InstalledBytes, string? Type, int? SpeedMts, int Modules);

public enum GpuKind
{
    Integrated,
    Dedicated,
}

/// <param name="Luid">Adapter id used by the "GPU Engine" performance counters.</param>
public sealed record GpuInfo(long Luid, string Name, GpuKind Kind, ulong DedicatedMemoryBytes);

/// <summary>What the machine has: names and capacities. Read once; does not change while running.</summary>
public sealed record HardwareInventory(SystemInfo System, CpuInfo Cpu, MemoryInfo Memory, IReadOnlyList<GpuInfo> Gpus);
