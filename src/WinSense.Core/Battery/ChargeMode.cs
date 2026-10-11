namespace WinSense.Core.Battery;

public enum ChargeMode
{
    /// <summary>Charges up to ~80% and then runs on AC power, preserving battery health.</summary>
    Optimized,

    /// <summary>Charges up to 100%.</summary>
    Full,
}
