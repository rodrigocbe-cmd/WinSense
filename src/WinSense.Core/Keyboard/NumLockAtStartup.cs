using System.Globalization;
using Microsoft.Win32;

namespace WinSense.Core.Keyboard;

/// <param name="Session">Num Lock after signing in (what the user sees day to day).</param>
/// <param name="LockScreen">Num Lock at power-on, on the lock screen.</param>
public sealed record NumLockState(bool Session, bool LockScreen)
{
    public bool IsMixed => Session != LockScreen;
}

public interface INumLockAtStartup
{
    NumLockState Read();

    /// <summary>Sets both the lock screen and the user session.</summary>
    void SetEnabled(bool enabled);
}

/// <summary>
/// Num Lock state at startup: <c>InitialKeyboardIndicators</c> under <c>HKEY_USERS\.DEFAULT</c> (power-on and
/// lock screen) and <c>HKEY_CURRENT_USER</c> (after signing in). Bit 1 (value 2) is Num Lock; other bits
/// (Caps Lock, Scroll Lock, and the 0x80000000 flag Windows uses with Fast Startup) are preserved.
/// Writing .DEFAULT requires administrator rights. HKCU is the elevated account's hive, which is the
/// signed-in user unless elevation used a different account's credentials.
/// </summary>
public sealed class NumLockAtStartup : INumLockAtStartup
{
    private const string KeyPath = @"Control Panel\Keyboard";
    private const string ValueName = "InitialKeyboardIndicators";
    private const uint NumLockBit = 0x2;

    public NumLockState Read() => new(
        Session: HasNumLock(ReadValue(Registry.CurrentUser, KeyPath)),
        LockScreen: HasNumLock(ReadValue(Registry.Users, @".DEFAULT\" + KeyPath)));

    public void SetEnabled(bool enabled)
    {
        Write(Registry.Users, @".DEFAULT\" + KeyPath, enabled);
        Write(Registry.CurrentUser, KeyPath, enabled);

        if (Read() != new NumLockState(enabled, enabled))
            throw new InvalidOperationException("O registro não ficou com o valor esperado.");
    }

    /// <summary>The value is a REG_SZ holding a decimal number (e.g. "2147483650" = 0x80000002).</summary>
    public static bool HasNumLock(string? value) => (Parse(value) & NumLockBit) != 0;

    public static string WithNumLock(string? value, bool enabled)
    {
        var flags = Parse(value);
        flags = enabled ? flags | NumLockBit : flags & ~NumLockBit;
        return flags.ToString(CultureInfo.InvariantCulture);
    }

    private static uint Parse(string? value) =>
        uint.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var flags) ? flags : 0;

    private static string? ReadValue(RegistryKey root, string path)
    {
        using var key = root.OpenSubKey(path);
        return key?.GetValue(ValueName)?.ToString();
    }

    private static void Write(RegistryKey root, string path, bool enabled)
    {
        try
        {
            using var key = root.CreateSubKey(path, writable: true);
            key.SetValue(ValueName, WithNumLock(key.GetValue(ValueName)?.ToString(), enabled), RegistryValueKind.String);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            throw new UnauthorizedAccessException("É preciso executar o WinSense como administrador.", ex);
        }
    }
}
