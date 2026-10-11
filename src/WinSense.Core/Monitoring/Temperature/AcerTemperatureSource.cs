using System.Management;

namespace WinSense.Core.Monitoring.Temperature;

/// <summary>
/// CPU and GPU temperatures from the Acer gaming WMI interface (root\WMI:AcerGamingFunction,
/// method GetGamingSysInfo), the same embedded-controller sensors NitroSense shows.
/// Command and result layout follow the Linux acer-wmi driver (Predator v4 sensor reading).
/// Requires administrator rights.
/// </summary>
public sealed class AcerTemperatureSource : ITemperatureSource, IDisposable
{
    private const string Scope = @"root\WMI";
    private const string ClassName = "AcerGamingFunction";
    private const string Method = "GetGamingSysInfo";
    private const uint ReadSensorCommand = 0x01;

    public const byte CpuSensor = 0x01;
    public const byte GpuSensor = 0x0A;

    private readonly ManagementObject _instance;

    private AcerTemperatureSource(ManagementObject instance) => _instance = instance;

    /// <summary>Returns the source if this machine has the interface and it answers with a sane CPU reading.</summary>
    public static AcerTemperatureSource? TryCreate()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(Scope, $"SELECT * FROM {ClassName}");
            var instance = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
            if (instance is null)
                return null;

            var source = new AcerTemperatureSource(instance);
            if (source.ReadSensor(CpuSensor) is not null)
                return source;

            source.Dispose();
            return null;
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

    public TemperatureReading Read() => new(ReadSensor(CpuSensor), ReadSensor(GpuSensor), null);

    /// <summary>Sensor index goes in bits 15:8 of the command.</summary>
    public static uint BuildCommand(byte sensor) => ReadSensorCommand | ((uint)sensor << 8);

    /// <summary>Bits 7:0 are a status (0 = ok) and bits 23:8 the reading in °C.</summary>
    public static double? DecodeTemperature(ulong result)
    {
        if ((result & 0xFF) != 0)
            return null;

        var celsius = (result >> 8) & 0xFFFF;
        // The EC answers 0 for sensors it does not have (e.g. the GPU while it is powered off).
        return celsius is > 0 and < 150 ? celsius : null;
    }

    private double? ReadSensor(byte sensor)
    {
        try
        {
            using var input = _instance.GetMethodParameters(Method);
            input["gmInput"] = BuildCommand(sensor);
            using var output = _instance.InvokeMethod(Method, input, null);
            return DecodeTemperature(Convert.ToUInt64(output["gmOutput"]));
        }
        catch (ManagementException)
        {
            return null;
        }
    }

    public void Dispose() => _instance.Dispose();
}
