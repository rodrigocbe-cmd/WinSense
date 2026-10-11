namespace WinSense.Core.Monitoring.Temperature;

/// <summary>
/// Windows has no general API for CPU/GPU temperature: real sensors sit behind the laptop's embedded
/// controller, which each vendor exposes its own way. One implementation per mechanism.
/// </summary>
public interface ITemperatureSource
{
    TemperatureReading Read();
}

public sealed class NoTemperatureSource : ITemperatureSource
{
    public TemperatureReading Read() => TemperatureReading.None;
}
