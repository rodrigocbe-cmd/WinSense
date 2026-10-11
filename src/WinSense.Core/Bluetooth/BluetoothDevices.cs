using System.Management;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace WinSense.Core.Bluetooth;

/// <summary>The groups Windows Settings uses for paired devices.</summary>
public enum DeviceCategory
{
    Input,
    Audio,
    Other,
}

/// <summary>How WinSense can make the PC connect to a device, if at all.</summary>
public enum ReconnectMethod
{
    /// <summary>No meaningful "connect" for this kind of device (e.g. a phone).</summary>
    None,

    /// <summary>Controllers, keyboards, mice: page it, then restart its HID service if needed.</summary>
    Hid,

    /// <summary>Headphones and speakers: the Bluetooth audio driver's reconnect (Settings › "Connect").</summary>
    Audio,
}

public sealed record PairedDevice(
    ulong Address, string Name, DeviceCategory Category, ReconnectMethod Method, bool IsConnected, bool IsGameController)
{
    public string AddressText => BluetoothAddress.Format(Address);

    public bool CanAutoConnect => Method != ReconnectMethod.None;
}

public enum ConnectResult
{
    /// <summary>The PC called the device and it connected.</summary>
    Connected,

    /// <summary>The controller answered but only connected after its HID service was restarted.</summary>
    ConnectedAfterServiceRestart,

    /// <summary>The device did not answer: it is off or out of range.</summary>
    NotReachable,

    /// <summary>The device answered but did not connect.</summary>
    Failed,
}

public interface IBluetoothDevices
{
    /// <summary>Paired classic Bluetooth devices.</summary>
    Task<IReadOnlyList<PairedDevice>> ListPairedAsync();

    Task<ConnectResult> TryConnectAsync(PairedDevice device, CancellationToken cancellationToken);
}

/// <summary>
/// Classic Bluetooth devices reconnect by calling the PC themselves; when one "forgets" the PC, Windows just
/// waits. This makes the PC call the device instead, per kind:
///   HID (controllers): A) an uncached SDP query pages the device; B) if it answers but the HID link does not
///     come up, the HID service is disabled and re-enabled, which makes the HID driver connect. B only runs
///     when A proved the device is on, so a device that is off does not get its driver reinstalled every cycle.
///   Audio: the A2DP driver's one-shot reconnect, the same request behind Settings › "Connect".
/// Needs administrator rights (HID service, audio driver).
/// </summary>
public sealed class WindowsBluetoothDevices : IBluetoothDevices
{
    private static readonly TimeSpan HidSettleTime = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan AudioSettleTime = TimeSpan.FromSeconds(6);

    public async Task<IReadOnlyList<PairedDevice>> ListPairedAsync()
    {
        var infos = await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true));
        var hidNodes = HidServiceNodes();
        var audioFilters = AudioReconnect.FilterPaths();

        var devices = new List<PairedDevice>();
        foreach (var info in infos)
        {
            using var device = await BluetoothDevice.FromIdAsync(info.Id);
            if (device is null)
                continue;

            var hex = device.BluetoothAddress.ToString("X12");
            var cod = device.ClassOfDevice.RawValue;
            var method = audioFilters.Any(p => p.Contains(hex, StringComparison.OrdinalIgnoreCase)) ? ReconnectMethod.Audio
                : hidNodes.Any(id => id.Contains(hex, StringComparison.OrdinalIgnoreCase)) ? ReconnectMethod.Hid
                : ReconnectMethod.None;

            devices.Add(new PairedDevice(device.BluetoothAddress, device.Name, BluetoothAddress.Categorize(cod), method,
                IsConnected(device, method, hidNodes), BluetoothAddress.IsGameController(cod)));
        }
        return devices.OrderBy(d => d.Category).ThenBy(d => d.Name).ThenBy(d => d.Address).ToList();
    }

    public Task<ConnectResult> TryConnectAsync(PairedDevice device, CancellationToken cancellationToken) => device.Method switch
    {
        ReconnectMethod.Hid => TryConnectHidAsync(device.Address, cancellationToken),
        ReconnectMethod.Audio => TryConnectAudioAsync(device.Address, cancellationToken),
        _ => Task.FromResult(ConnectResult.Failed),
    };

    private static async Task<ConnectResult> TryConnectHidAsync(ulong address, CancellationToken cancellationToken)
    {
        using var device = await BluetoothDevice.FromBluetoothAddressAsync(address);
        if (device is null)
            return ConnectResult.Failed;

        // A: page the device.
        var query = await device.GetRfcommServicesAsync(BluetoothCacheMode.Uncached).AsTask(cancellationToken);
        if (query.Error != BluetoothError.Success)
            return ConnectResult.NotReachable;

        await Task.Delay(HidSettleTime, cancellationToken);
        if (await IsConnectedAsync(address, ReconnectMethod.Hid))
            return ConnectResult.Connected;

        // B: it is on and answering, but the controller part did not connect.
        HidService.Restart(address);
        await Task.Delay(HidSettleTime, cancellationToken);
        return await IsConnectedAsync(address, ReconnectMethod.Hid) ? ConnectResult.ConnectedAfterServiceRestart : ConnectResult.Failed;
    }

    private static async Task<ConnectResult> TryConnectAudioAsync(ulong address, CancellationToken cancellationToken)
    {
        if (!AudioReconnect.Request(address))
            return ConnectResult.Failed;

        // The driver pages the device in the background; give it time before checking.
        await Task.Delay(AudioSettleTime, cancellationToken);
        return await IsConnectedAsync(address, ReconnectMethod.Audio) ? ConnectResult.Connected : ConnectResult.NotReachable;
    }

    private static async Task<bool> IsConnectedAsync(ulong address, ReconnectMethod method)
    {
        using var device = await BluetoothDevice.FromBluetoothAddressAsync(address);
        return device is not null && IsConnected(device, method, HidServiceNodes());
    }

    /// <summary>
    /// The radio link alone also comes up during a plain SDP query, so HID devices additionally need their
    /// HID device present: the BTHENUM\{00001124...} service node stays "OK" while the controller is off;
    /// what appears and disappears is its child HID\... device.
    /// </summary>
    private static bool IsConnected(BluetoothDevice device, ReconnectMethod method, IReadOnlyList<string> hidNodes)
    {
        if (device.ConnectionStatus != BluetoothConnectionStatus.Connected)
            return false;
        if (method != ReconnectMethod.Hid)
            return true;

        var hex = device.BluetoothAddress.ToString("X12");
        return hidNodes.Any(id => id.Contains(hex, StringComparison.OrdinalIgnoreCase) && DeviceNodes.HasPresentChild(id));
    }

    /// <summary>Device instance ids of every paired device's HID service node.</summary>
    private static IReadOnlyList<string> HidServiceNodes()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT DeviceID FROM Win32_PnPEntity WHERE PNPClass = 'HIDClass'");
        var ids = new List<string>();
        foreach (var entity in searcher.Get().Cast<ManagementObject>())
        {
            using (entity)
            {
                if (entity["DeviceID"] is string id && id.StartsWith(HidService.PnpPrefix, StringComparison.OrdinalIgnoreCase))
                    ids.Add(id);
            }
        }
        return ids;
    }
}

public static class BluetoothAddress
{
    /// <summary>0x4142F3F52138 → "41:42:F3:F5:21:38".</summary>
    public static string Format(ulong address) =>
        string.Join(":", Enumerable.Range(0, 6).Select(i => ((address >> (8 * (5 - i))) & 0xFF).ToString("X2")));

    /// <summary>Class of Device major class (bits 12..8): 5 = peripheral (input), 4 = audio/video.</summary>
    public static DeviceCategory Categorize(uint classOfDevice) => ((classOfDevice >> 8) & 0x1F) switch
    {
        0x05 => DeviceCategory.Input,
        0x04 => DeviceCategory.Audio,
        _ => DeviceCategory.Other,
    };

    /// <summary>
    /// Peripheral (major 5) whose minor class (bits 7..2) has device type 1 = joystick or 2 = gamepad in its
    /// low nibble; the high bits flag keyboard/pointer combos.
    /// </summary>
    public static bool IsGameController(uint classOfDevice)
    {
        var major = (classOfDevice >> 8) & 0x1F;
        var minorType = (classOfDevice >> 2) & 0x0F;
        return major == 0x05 && minorType is 0x01 or 0x02;
    }
}
