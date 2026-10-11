using System.IO;
using System.Text.Json;
using WinSense.Core.Bluetooth;

namespace WinSense.App;

/// <summary>User preferences, in %LocalAppData%\WinSense\settings.json.</summary>
/// <param name="BluetoothAutoConnect">The main switch of "Bluetooth e Dispositivos".</param>
/// <param name="AutoConnectChoices">Per device ("41:42:F3:F5:21:38" → on/off); devices not listed use the default.</param>
public sealed record AppSettings(bool BluetoothAutoConnect = false, IReadOnlyDictionary<string, bool>? AutoConnectChoices = null)
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinSense", "settings.json");

    /// <summary>The user's choice, or by default: on for game controllers, off for everything else.</summary>
    public bool ShouldAutoConnect(PairedDevice device) =>
        AutoConnectChoices is not null && AutoConnectChoices.TryGetValue(device.AddressText, out var chosen)
            ? chosen
            : device.IsGameController;

    public AppSettings WithChoice(PairedDevice device, bool enabled)
    {
        var choices = new Dictionary<string, bool>(AutoConnectChoices ?? new Dictionary<string, bool>())
        {
            [device.AddressText] = enabled,
        };
        return this with { AutoConnectChoices = choices };
    }

    public static AppSettings Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception)
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
    }
}
