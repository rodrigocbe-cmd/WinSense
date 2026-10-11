using System.Management;
using System.Text.RegularExpressions;
using WinSense.Core.Monitoring.Native;

namespace WinSense.Core.Monitoring;

public static partial class HardwareInventoryReader
{
    private const uint VendorNvidia = 0x10DE;
    private const uint VendorAmd = 0x1002;
    private const uint VendorIntel = 0x8086;

    public static HardwareInventory Read() => new(ReadSystem(), ReadCpu(), ReadMemory(), ReadGpus());

    private static SystemInfo ReadSystem()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT DNSHostName, Manufacturer, Model, SystemFamily FROM Win32_ComputerSystem");
        using var system = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
        if (system is null)
            return new SystemInfo(Environment.MachineName, null);

        var name = system["DNSHostName"] as string;
        return new SystemInfo(
            string.IsNullOrWhiteSpace(name) ? Environment.MachineName : name,
            DescribeModel(system["Manufacturer"] as string, system["Model"] as string, system["SystemFamily"] as string));
    }

    /// <summary>
    /// "Acer" + "Nitro ANV15-41" + family "Acer Nitro V 15" → "Acer Nitro V 15 (Nitro ANV15-41)".
    /// Ignores the placeholders that generic firmware leaves in these fields.
    /// </summary>
    public static string? DescribeModel(string? manufacturer, string? model, string? family)
    {
        manufacturer = Meaningful(manufacturer);
        model = Meaningful(model);
        family = Meaningful(family);

        var name = family is not null && model is not null && !family.Contains(model, StringComparison.OrdinalIgnoreCase)
            ? $"{family} ({model})"
            : family ?? model;
        if (name is null)
            return manufacturer;

        return manufacturer is null || name.StartsWith(manufacturer, StringComparison.OrdinalIgnoreCase)
            ? name
            : $"{manufacturer} {name}";
    }

    private static string? Meaningful(string? value)
    {
        value = value is null ? null : MultipleSpaces().Replace(value, " ").Trim();
        return string.IsNullOrEmpty(value) || PlaceholderNames.Contains(value) ? null : value;
    }

    private static readonly HashSet<string> PlaceholderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Default string", "To be filled by O.E.M.", "O.E.M.", "OEM",
        "System manufacturer", "System Product Name", "System Version", "Not Applicable", "None", "Unknown",
    };

    private static CpuInfo ReadCpu()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor");
        var cpus = searcher.Get().Cast<ManagementObject>().ToList();
        try
        {
            var first = cpus.FirstOrDefault();
            if (first is null)
                return new CpuInfo("Processador desconhecido", 0, Environment.ProcessorCount, 0);

            return new CpuInfo(
                CleanCpuName(first["Name"] as string ?? ""),
                cpus.Sum(c => Convert.ToInt32(c["NumberOfCores"])),
                cpus.Sum(c => Convert.ToInt32(c["NumberOfLogicalProcessors"])),
                Convert.ToInt32(first["MaxClockSpeed"]));
        }
        finally
        {
            cpus.ForEach(c => c.Dispose());
        }
    }

    private static MemoryInfo ReadMemory()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT Capacity, ConfiguredClockSpeed, Speed, SMBIOSMemoryType FROM Win32_PhysicalMemory");
        var modules = searcher.Get().Cast<ManagementObject>().ToList();
        try
        {
            var installed = modules.Aggregate(0UL, (sum, m) => sum + Convert.ToUInt64(m["Capacity"]));
            if (installed == 0)
                installed = MemoryStatus.Read().Total;

            var first = modules.FirstOrDefault();
            int? speed = first is null ? null : NonZero(first["ConfiguredClockSpeed"]) ?? NonZero(first["Speed"]);
            var type = first is null ? null : MemoryTypeName(Convert.ToInt32(first["SMBIOSMemoryType"]));
            return new MemoryInfo(installed, type, speed, modules.Count);
        }
        finally
        {
            modules.ForEach(m => m.Dispose());
        }
    }

    private static IReadOnlyList<GpuInfo> ReadGpus() =>
        Dxgi.EnumerateHardwareAdapters()
            .Select(a => new GpuInfo(a.Luid, a.Description, ClassifyGpu(a.VendorId, a.Description), a.DedicatedVideoMemory))
            .OrderBy(g => g.Kind == GpuKind.Dedicated ? 0 : 1)
            .ToList();

    /// <summary>Decodes SMBIOS memory type codes (SMBIOS spec 3.x, table "Memory Device — Type").</summary>
    public static string? MemoryTypeName(int smbiosType) => smbiosType switch
    {
        20 => "DDR",
        21 => "DDR2",
        24 => "DDR3",
        26 => "DDR4",
        27 => "LPDDR",
        28 => "LPDDR2",
        29 => "LPDDR3",
        30 => "LPDDR4",
        34 => "DDR5",
        35 => "LPDDR5",
        _ => null,
    };

    /// <summary>
    /// There is no reliable API flag for integrated vs. discrete without creating a device (which would
    /// wake a sleeping GPU), so this goes by vendor and product name.
    /// </summary>
    public static GpuKind ClassifyGpu(uint vendorId, string name) => vendorId switch
    {
        VendorNvidia => GpuKind.Dedicated,
        VendorAmd => AmdDiscreteName().IsMatch(name) ? GpuKind.Dedicated : GpuKind.Integrated,
        VendorIntel => name.Contains("Arc", StringComparison.OrdinalIgnoreCase)
                       && !name.Contains("Graphics", StringComparison.OrdinalIgnoreCase)
            ? GpuKind.Dedicated
            : GpuKind.Integrated,
        _ => GpuKind.Dedicated,
    };

    /// <summary>"AMD Ryzen 7 7735HS with Radeon Graphics        " → "AMD Ryzen 7 7735HS with Radeon Graphics".</summary>
    public static string CleanCpuName(string name) => MultipleSpaces().Replace(name, " ").Trim();

    private static int? NonZero(object? value) =>
        value is null ? null : Convert.ToInt32(value) is var v && v > 0 ? v : null;

    // "Radeon RX 7600M XT", "Radeon Pro W7600"; integrated ones are "Radeon(TM) Graphics",
    // "Radeon 780M Graphics", "Radeon Vega 8 Graphics".
    [GeneratedRegex(@"\b(RX|Pro)\b", RegexOptions.IgnoreCase)]
    private static partial Regex AmdDiscreteName();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultipleSpaces();
}
