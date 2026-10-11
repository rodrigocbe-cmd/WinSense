using System.Management;

namespace WinSense.Core.Monitoring.Temperature;

/// <summary>
/// Fallback: the ACPI thermal zone (root\WMI:MSAcpi_ThermalZoneTemperature). It is a motherboard
/// zone defined by the firmware, not the CPU die sensor, so it is shown as "system" temperature.
/// Many machines do not implement it. Requires administrator rights.
/// </summary>
public sealed class AcpiThermalZoneSource : ITemperatureSource
{
    private const string Query = "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature";

    public static AcpiThermalZoneSource? TryCreate() =>
        ReadHottestZone() is not null ? new AcpiThermalZoneSource() : null;

    public TemperatureReading Read() => new(null, null, ReadHottestZone());

    /// <summary>The value is in tenths of a kelvin.</summary>
    public static double? KelvinTenthsToCelsius(uint value)
    {
        var celsius = value / 10.0 - 273.15;
        return celsius is > 0 and < 150 ? Math.Round(celsius, 1) : null;
    }

    private static double? ReadHottestZone()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", Query);
            return searcher.Get().Cast<ManagementObject>()
                .Select(zone =>
                {
                    using (zone)
                        return KelvinTenthsToCelsius(Convert.ToUInt32(zone["CurrentTemperature"]));
                })
                .Where(t => t is not null)
                .Max();
        }
        catch (ManagementException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
