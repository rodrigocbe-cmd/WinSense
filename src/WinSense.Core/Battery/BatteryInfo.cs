using System.Management;

namespace WinSense.Core.Battery;

public sealed record BatteryInfo(int ChargePercent, bool IsPluggedIn)
{
    private const ushort BatteryStatusOnAc = 2;

    /// <summary>Reads the current charge level. Does not require administrator rights.</summary>
    public static BatteryInfo? Read()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT EstimatedChargeRemaining, BatteryStatus FROM Win32_Battery");
        using var battery = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
        if (battery is null)
            return null;

        return new BatteryInfo(
            Convert.ToInt32(battery["EstimatedChargeRemaining"]),
            Convert.ToUInt16(battery["BatteryStatus"]) == BatteryStatusOnAc);
    }
}
