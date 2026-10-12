namespace WinSense.Core.Battery;

public static class BatteryChargeControllerFactory
{
    /// <summary>Picks the vendor implementation available on this machine. Add new vendors here.</summary>
    public static IBatteryChargeController Create()
    {
        if (AcerBatteryChargeController.IsAvailable())
            return new AcerBatteryChargeController();

        return new UnsupportedBatteryChargeController(
            "Este computador não oferece um controle de limite de carga compatível.");
    }
}
