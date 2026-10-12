using System.Management;

namespace WinSense.Core.Battery;

/// <summary>
/// Acer "Battery Charge Limit" (the 80% limit in NitroSense / Acer Care Center), driven through
/// the vendor WMI class root\WMI:BatteryControl. Parameter layout follows the
/// acer-wmi-battery Linux driver (https://github.com/frederik-h/acer-wmi-battery).
/// Requires administrator rights.
/// </summary>
public sealed class AcerBatteryChargeController : IBatteryChargeController
{
    private const string Scope = @"root\WMI";
    private const string ClassName = "BatteryControl";
    private const byte BatteryNumber = 1;
    private const byte HealthModeFunction = 0x01;

    public bool IsSupported => true;

    public string ProviderName => "Acer (BatteryControl WMI)";

    public static bool IsAvailable()
    {
        try
        {
            using var wmiClass = new ManagementClass(Scope, ClassName, null);
            wmiClass.Get();
            return true;
        }
        catch (ManagementException)
        {
            return false;
        }
    }

    public ChargeMode GetMode()
    {
        using var instance = GetInstance();
        using var input = instance.GetMethodParameters("GetBatteryHealthControlStatus");
        input["uBatteryNo"] = BatteryNumber;
        input["uFunctionQuery"] = HealthModeFunction;
        input["uReserved"] = new byte[2];

        using var output = instance.InvokeMethod("GetBatteryHealthControlStatus", input, null);
        var status = (byte[])output["uFunctionStatus"];
        return status[0] > 0 ? ChargeMode.Optimized : ChargeMode.Full;
    }

    public void SetMode(ChargeMode mode)
    {
        using (var instance = GetInstance())
        using (var input = instance.GetMethodParameters("SetBatteryHealthControl"))
        {
            input["uBatteryNo"] = BatteryNumber;
            input["uFunctionMask"] = HealthModeFunction;
            input["uFunctionStatus"] = mode == ChargeMode.Optimized ? (byte)1 : (byte)0;
            input["uReservedIn"] = new byte[5];

            using var output = instance.InvokeMethod("SetBatteryHealthControl", input, null);
            var code = Convert.ToInt32(output["uReturn"]);
            if (code != 0)
                throw new InvalidOperationException($"O firmware recusou a alteração (código {code}).");
        }

        // Trust the firmware, not the return code: read back what is actually in effect.
        if (GetMode() != mode)
            throw new InvalidOperationException("O firmware aceitou o comando, mas o modo não mudou.");
    }

    private static ManagementObject GetInstance()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(Scope, $"SELECT * FROM {ClassName}");
            return searcher.Get().Cast<ManagementObject>().FirstOrDefault()
                ?? throw new InvalidOperationException("Interface de bateria da Acer não encontrada.");
        }
        catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.AccessDenied)
        {
            throw new UnauthorizedAccessException("É preciso executar o WinSense como administrador.", ex);
        }
    }
}
