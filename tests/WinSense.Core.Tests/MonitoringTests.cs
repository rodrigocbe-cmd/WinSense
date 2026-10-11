using WinSense.Core.Monitoring;
using WinSense.Core.Monitoring.Temperature;

namespace WinSense.Core.Tests;

public class GpuCountersTests
{
    private const long Igpu = 0x111D0;
    private const long Dgpu = 0x1314F;

    [Fact]
    public void Usage_SumsProcessesPerEngine_AndTakesBusiestEngine()
    {
        var samples = new[]
        {
            ("pid_100_luid_0x00000000_0x000111D0_phys_0_eng_0_engtype_3D", 20.0),
            ("pid_200_luid_0x00000000_0x000111D0_phys_0_eng_0_engtype_3D", 15.0),
            ("pid_100_luid_0x00000000_0x000111D0_phys_0_eng_1_engtype_Copy", 30.0),
            ("pid_300_luid_0x00000000_0x0001314F_phys_0_eng_0_engtype_3D", 5.0),
        };

        var usage = GpuCounters.UsageByLuid(samples);

        Assert.Equal(35.0, usage[Igpu]); // 3D engine: 20 + 15 beats Copy: 30
        Assert.Equal(5.0, usage[Dgpu]);
    }

    [Fact]
    public void Usage_IsCappedAt100_AndIgnoresUnknownInstances()
    {
        var samples = new[]
        {
            ("pid_1_luid_0x00000000_0x000111D0_phys_0_eng_0_engtype_3D", 70.0),
            ("pid_2_luid_0x00000000_0x000111D0_phys_0_eng_0_engtype_3D", 60.0),
            ("_Total", 99.0),
        };

        var usage = GpuCounters.UsageByLuid(samples);

        Assert.Equal(100.0, usage[Igpu]);
        Assert.Single(usage);
    }

    [Fact]
    public void Memory_ParsesAdapterInstances()
    {
        var memory = GpuCounters.MemoryByLuid([("luid_0x00000000_0x000111D0_phys_0", 512.0 * 1024 * 1024)]);

        Assert.Equal(512UL * 1024 * 1024, memory[Igpu]);
    }

    [Fact]
    public void Luid_UsesHighPart()
    {
        var usage = GpuCounters.UsageByLuid([("pid_1_luid_0x00000001_0x00000002_phys_0_eng_0_engtype_3D", 1.0)]);

        Assert.True(usage.ContainsKey(0x1_0000_0002));
    }
}

public class AcerTemperatureTests
{
    [Fact]
    public void Command_PutsSensorInSecondByte()
    {
        Assert.Equal(0x0101u, AcerTemperatureSource.BuildCommand(AcerTemperatureSource.CpuSensor));
        Assert.Equal(0x0A01u, AcerTemperatureSource.BuildCommand(AcerTemperatureSource.GpuSensor));
    }

    [Fact]
    public void Decode_ReadsBits8To23()
    {
        Assert.Equal(62.0, AcerTemperatureSource.DecodeTemperature(62UL << 8));
    }

    [Fact]
    public void Decode_ErrorStatus_IsNull()
    {
        Assert.Null(AcerTemperatureSource.DecodeTemperature((62UL << 8) | 0x01));
    }

    [Fact]
    public void Decode_ZeroOrAbsurd_IsNull()
    {
        Assert.Null(AcerTemperatureSource.DecodeTemperature(0));
        Assert.Null(AcerTemperatureSource.DecodeTemperature(500UL << 8));
    }

    [Fact]
    public void Acpi_ConvertsTenthsOfKelvin()
    {
        Assert.Equal(46.9, AcpiThermalZoneSource.KelvinTenthsToCelsius(3200));
        Assert.Null(AcpiThermalZoneSource.KelvinTenthsToCelsius(0));
    }
}

public class HardwareInventoryReaderTests
{
    [Theory]
    [InlineData(26, "DDR4")]
    [InlineData(34, "DDR5")]
    [InlineData(35, "LPDDR5")]
    [InlineData(0, null)]
    public void MemoryType(int smbios, string? expected) =>
        Assert.Equal(expected, HardwareInventoryReader.MemoryTypeName(smbios));

    [Theory]
    [InlineData(0x10DE, "NVIDIA GeForce RTX 4050 Laptop GPU", GpuKind.Dedicated)]
    [InlineData(0x1002, "AMD Radeon(TM) Graphics", GpuKind.Integrated)]
    [InlineData(0x1002, "AMD Radeon 780M Graphics", GpuKind.Integrated)]
    [InlineData(0x1002, "AMD Radeon Vega 8 Graphics", GpuKind.Integrated)]
    [InlineData(0x1002, "AMD Radeon RX 7600M XT", GpuKind.Dedicated)]
    [InlineData(0x8086, "Intel(R) UHD Graphics", GpuKind.Integrated)]
    [InlineData(0x8086, "Intel(R) Arc(TM) Graphics", GpuKind.Integrated)]
    [InlineData(0x8086, "Intel(R) Arc(TM) A770", GpuKind.Dedicated)]
    public void GpuKindByVendorAndName(uint vendor, string name, GpuKind expected) =>
        Assert.Equal(expected, HardwareInventoryReader.ClassifyGpu(vendor, name));

    [Theory]
    [InlineData("Acer", "Nitro ANV15-41", "Acer Nitro V 15", "Acer Nitro V 15 (Nitro ANV15-41)")]
    [InlineData("LENOVO", "82JW", "Legion 5 15ACH6H", "LENOVO Legion 5 15ACH6H (82JW)")]
    [InlineData("Dell Inc.", "XPS 15 9520", null, "Dell Inc. XPS 15 9520")]
    [InlineData("Micro-Star International Co., Ltd.", "MS-7C91", "Default string", "Micro-Star International Co., Ltd. MS-7C91")]
    [InlineData("System manufacturer", "System Product Name", "To be filled by O.E.M.", null)]
    public void ModelDescription(string? manufacturer, string? model, string? family, string? expected) =>
        Assert.Equal(expected, HardwareInventoryReader.DescribeModel(manufacturer, model, family));

    [Fact]
    public void CpuName_CollapsesPadding() =>
        Assert.Equal("AMD Ryzen 7 7735HS with Radeon Graphics",
            HardwareInventoryReader.CleanCpuName("AMD Ryzen 7 7735HS with Radeon Graphics        "));
}
