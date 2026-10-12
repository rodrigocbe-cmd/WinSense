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

    /// <summary>Controllers, keyboards, mice: when the device itself calls the PC, restart its HID service if needed.</summary>
    Hid,

    /// <summary>Headphones and speakers: the Bluetooth audio driver's reconnect (Settings › "Connect").</summary>
    Audio,
}

/// <param name="IsLinkUp">The radio link is up. For a HID device that is not connected, it means the device is on
/// and calling the PC, but its HID part did not come up.</param>
public sealed record PairedDevice(
    ulong Address, string Name, DeviceCategory Category, ReconnectMethod Method, bool IsConnected, bool IsGameController,
    bool IsLinkUp = false)
{
    public string AddressText => BluetoothAddress.Format(Address);

    public bool CanAutoConnect => Method != ReconnectMethod.None;

    /// <summary>
    /// Worth an attempt without calling it. HID devices only when they opened the link themselves: paging one that
    /// is off turns it on (seen with non-original DualShock 4s), so WinSense calls them only if the user allowed it.
    /// </summary>
    public bool IsAvailable => Method != ReconnectMethod.Hid || IsLinkUp;
}

public enum ConnectResult
{
    /// <summary>The PC called the device and it connected.</summary>
    Connected,

    /// <summary>The controller answered but only connected after its HID service was restarted.</summary>
    ConnectedAfterServiceRestart,

    /// <summary>The device did not answer, or dropped the link: it is off or out of range.</summary>
    NotReachable,

    /// <summary>The device answered but did not connect.</summary>
    Failed,

    /// <summary>The device had forgotten the PC (it was in pairing mode); it was paired again.</summary>
    Repaired,
}

public interface IBluetoothDevices
{
    /// <summary>Paired classic Bluetooth devices.</summary>
    Task<IReadOnlyList<PairedDevice>> ListPairedAsync();

    /// <param name="call">For HID devices whose link is down: page them. It can turn on a controller that is off.</param>
    Task<ConnectResult> TryConnectAsync(PairedDevice device, bool call, CancellationToken cancellationToken);

    /// <summary>Calls <paramref name="connected"/> when a paired device opens a radio link. Best effort; dispose to stop.</summary>
    IDisposable WatchConnections(Action connected);

    /// <summary>Paired devices in pairing mode right now (they forgot the PC). Takes a few seconds of radio time.</summary>
    Task<IReadOnlySet<ulong>> FindInPairingModeAsync(CancellationToken cancellationToken);

    /// <summary>Removes the pairing of a device in pairing mode and pairs it again.</summary>
    Task<ConnectResult> RepairAsync(PairedDevice device, CancellationToken cancellationToken);
}

/// <summary>
/// Classic Bluetooth devices reconnect by calling the PC themselves. Per kind:
///   HID (controllers): not paged unless the user asked for it, because paging a non-original DualShock 4 that is
///     off turns it on. When the controller calls the PC but its HID part does not come up, the HID service is
///     disabled and re-enabled, which makes the HID driver connect.
///   Audio: the A2DP driver's one-shot reconnect, the same request behind Settings › "Connect".
///   Any kind, when the device forgot the PC and is in pairing mode: unpair and pair again.
/// Needs administrator rights (HID service, audio driver); pairing does not.
/// </summary>
public sealed class WindowsBluetoothDevices : IBluetoothDevices
{
    private static readonly TimeSpan HidSettleTime = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan AudioSettleTime = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan RediscoverTimeout = TimeSpan.FromSeconds(20);

    public async Task<IReadOnlyList<PairedDevice>> ListPairedAsync()
    {
        var infos = await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true));
        var hidNodes = HidServiceNodes();
        var audioFilters = AudioReconnect.FilterPaths();

        var devices = new List<PairedDevice>();
        foreach (var info in infos)
        {
            using var device = await FromIdOrNullAsync(info.Id);
            if (device is null)
                continue;

            var hex = device.BluetoothAddress.ToString("X12");
            var cod = device.ClassOfDevice.RawValue;
            var method = audioFilters.Any(p => p.Contains(hex, StringComparison.OrdinalIgnoreCase)) ? ReconnectMethod.Audio
                : hidNodes.Any(id => id.Contains(hex, StringComparison.OrdinalIgnoreCase)) ? ReconnectMethod.Hid
                : ReconnectMethod.None;

            devices.Add(new PairedDevice(device.BluetoothAddress, device.Name, BluetoothAddress.Categorize(cod), method,
                IsConnected(device, method, hidNodes), BluetoothAddress.IsGameController(cod),
                device.ConnectionStatus == BluetoothConnectionStatus.Connected));
        }
        return devices.OrderBy(d => d.Category).ThenBy(d => d.Name).ThenBy(d => d.Address).ToList();
    }

    /// <summary>
    /// Windows keeps pairings made with a previous radio (e.g. another USB dongle) in the paired list; their id
    /// names that radio's address and FromIdAsync throws "invalid id" for them. Skip those instead of failing the list.
    /// </summary>
    private static async Task<BluetoothDevice?> FromIdOrNullAsync(string id)
    {
        try
        {
            return await BluetoothDevice.FromIdAsync(id);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public Task<ConnectResult> TryConnectAsync(PairedDevice device, bool call, CancellationToken cancellationToken) => device.Method switch
    {
        ReconnectMethod.Hid => TryConnectHidAsync(device.Address, call, cancellationToken),
        ReconnectMethod.Audio => TryConnectAudioAsync(device.Address, cancellationToken),
        _ => Task.FromResult(ConnectResult.Failed),
    };

    /// <summary>
    /// For a controller that called the PC itself (link up, HID down), or one the user allowed WinSense to call.
    /// The HID service restart only runs when the controller is on, so a controller that is off does not get its
    /// driver reinstalled every round.
    /// </summary>
    private static async Task<ConnectResult> TryConnectHidAsync(ulong address, bool call, CancellationToken cancellationToken)
    {
        using (var device = await BluetoothDevice.FromBluetoothAddressAsync(address))
        {
            if (device is null)
                return ConnectResult.Failed;

            // An uncached SDP query pages the device.
            if (call && device.ConnectionStatus != BluetoothConnectionStatus.Connected)
            {
                var query = await device.GetRfcommServicesAsync(BluetoothCacheMode.Uncached).AsTask(cancellationToken);
                if (query.Error != BluetoothError.Success)
                    return ConnectResult.NotReachable;
            }

            // A controller that just connected needs a moment for its HID part to come up.
            await Task.Delay(HidSettleTime, cancellationToken);
            if (IsConnected(device, ReconnectMethod.Hid, HidServiceNodes()))
                return ConnectResult.Connected;
            if (device.ConnectionStatus != BluetoothConnectionStatus.Connected)
                return ConnectResult.NotReachable; // gave up and turned off
        }

        // It is on and calling, but the controller part did not connect.
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

    public Task<IReadOnlySet<ulong>> FindInPairingModeAsync(CancellationToken cancellationToken) =>
        Task.Run(PairingModeScanner.Find, cancellationToken);

    /// <summary>
    /// Unpair, find the device again among unpaired ones and pair with automatic confirmation, as Settings does for
    /// controllers and headsets. Tested with a non-original DualShock 4 (Just Works, ConfirmOnly).
    /// </summary>
    public async Task<ConnectResult> RepairAsync(PairedDevice device, CancellationToken cancellationToken)
    {
        using (var paired = await BluetoothDevice.FromBluetoothAddressAsync(device.Address))
        {
            if (paired is null)
                return ConnectResult.Failed;
            if (paired.DeviceInformation.Pairing.IsPaired)
            {
                var unpair = await paired.DeviceInformation.Pairing.UnpairAsync().AsTask(cancellationToken);
                if (unpair.Status is not (DeviceUnpairingResultStatus.Unpaired or DeviceUnpairingResultStatus.AlreadyUnpaired))
                    throw new InvalidOperationException($"o Windows não removeu o pareamento antigo ({unpair.Status}).");
            }
        }

        var found = await RediscoverAsync(device.Address, cancellationToken)
            ?? throw new InvalidOperationException(
                "o pareamento antigo foi removido, mas o dispositivo saiu do modo de pareamento. Pareie de novo nas Configurações.");

        var pairing = found.Pairing.Custom;
        pairing.PairingRequested += (_, request) =>
        {
            // Old headsets ask for a fixed PIN, nearly always 0000.
            if (request.PairingKind == DevicePairingKinds.ProvidePin)
                request.Accept("0000");
            else
                request.Accept();
        };
        var result = await pairing.PairAsync(
            DevicePairingKinds.ConfirmOnly | DevicePairingKinds.ConfirmPinMatch | DevicePairingKinds.DisplayPin | DevicePairingKinds.ProvidePin,
            DevicePairingProtectionLevel.Default).AsTask(cancellationToken);
        return result.Status is DevicePairingResultStatus.Paired or DevicePairingResultStatus.AlreadyPaired
            ? ConnectResult.Repaired
            : throw new InvalidOperationException(
                $"o pareamento antigo foi removido, mas parear de novo falhou ({result.Status}). Pareie de novo nas Configurações.");
    }

    /// <summary>An unpaired device only shows up again through discovery (an inquiry).</summary>
    private static async Task<DeviceInformation?> RediscoverAsync(ulong address, CancellationToken cancellationToken)
    {
        var suffix = "-" + BluetoothAddress.Format(address).ToLowerInvariant();
        var found = new TaskCompletionSource<DeviceInformation>(TaskCreationOptions.RunContinuationsAsynchronously);
        var watcher = DeviceInformation.CreateWatcher(BluetoothDevice.GetDeviceSelectorFromPairingState(false),
            null, DeviceInformationKind.AssociationEndpoint);
        watcher.Added += (_, info) =>
        {
            if (info.Id.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                found.TrySetResult(info);
        };
        watcher.Updated += (_, _) => { };
        watcher.Start();
        try
        {
            return await found.Task.WaitAsync(RediscoverTimeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            return null;
        }
        finally
        {
            if (watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
                watcher.Stop();
        }
    }

    /// <summary>
    /// Watches paired devices whose link comes up, so a controller calling the PC is handled right away instead of
    /// on the next round (its link may not stay up for long).
    /// </summary>
    public IDisposable WatchConnections(Action connected)
    {
        var watcher = DeviceInformation.CreateWatcher(
            BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected),
            null, DeviceInformationKind.AssociationEndpoint);
        watcher.Added += (_, _) => connected();
        watcher.Updated += (_, _) => { }; // the watcher needs a handler here to report changes
        watcher.Start();
        return new WatcherStopper(watcher);
    }

    private sealed class WatcherStopper(DeviceWatcher watcher) : IDisposable
    {
        public void Dispose()
        {
            if (watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
                watcher.Stop();
        }
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
