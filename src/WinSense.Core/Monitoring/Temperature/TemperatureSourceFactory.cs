namespace WinSense.Core.Monitoring.Temperature;

public static class TemperatureSourceFactory
{
    /// <summary>Picks the best sensor source available on this machine. Add new vendors here.</summary>
    public static ITemperatureSource Create() =>
        (ITemperatureSource?)AcerTemperatureSource.TryCreate()
        ?? (ITemperatureSource?)AcpiThermalZoneSource.TryCreate()
        ?? new NoTemperatureSource();
}
