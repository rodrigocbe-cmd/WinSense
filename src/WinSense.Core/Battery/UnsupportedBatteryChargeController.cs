namespace WinSense.Core.Battery;

public sealed class UnsupportedBatteryChargeController(string reason) : IBatteryChargeController
{
    public bool IsSupported => false;

    public string ProviderName => reason;

    public ChargeMode GetMode() => throw new NotSupportedException(reason);

    public void SetMode(ChargeMode mode) => throw new NotSupportedException(reason);
}
