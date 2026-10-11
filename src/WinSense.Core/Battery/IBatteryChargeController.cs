namespace WinSense.Core.Battery;

/// <summary>
/// Controls the battery charge limit. There is no Windows API for this: each laptop
/// vendor exposes it through its own firmware interface, so each vendor gets an implementation.
/// </summary>
public interface IBatteryChargeController
{
    bool IsSupported { get; }

    /// <summary>Which mechanism is in use, or why the feature is unavailable.</summary>
    string ProviderName { get; }

    ChargeMode GetMode();

    void SetMode(ChargeMode mode);
}
