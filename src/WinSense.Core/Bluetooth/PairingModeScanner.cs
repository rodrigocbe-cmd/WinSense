using System.Runtime.InteropServices;

namespace WinSense.Core.Bluetooth;

/// <summary>
/// Finds paired devices that are in pairing mode, i.e. that "forgot" the PC (a DualShock 4 used on another host,
/// or one that lost its key). Windows hides paired devices from discovery results, but during an inquiry the
/// radio reports every device found through GUID_BLUETOOTH_RADIO_IN_RANGE, paired ones included, with
/// BDIF_VISIBLE set. Nothing is paged, so devices that are off stay off. No administrator rights needed.
/// </summary>
internal static class PairingModeScanner
{
    private static readonly Guid RadioInRange = new("EA3B5B82-26EE-450E-B0D8-D26FE30A3869");

    private const uint PairedFlag = 0x08;   // BDIF_PAIRED
    private const uint VisibleFlag = 0x80;  // BDIF_VISIBLE

    /// <summary>Inquiry length in units of 1.28 s.</summary>
    private const byte InquiryLength = 2;

    private const int FilterTypeDeviceHandle = 1;
    private const int ActionCustomEvent = 6;
    private const int FilterSize = 416;    // CM_NOTIFY_FILTER
    private const int EventDataOffset = 32; // CM_NOTIFY_EVENT_DATA.u.DeviceHandle.Data
    private const int AddressOffset = 8;    // BTH_DEVICE_INFO.address

    /// <summary>Addresses of paired devices that answered an inquiry (are discoverable) now. Blocks a few seconds.</summary>
    public static IReadOnlySet<ulong> Find()
    {
        var found = new HashSet<ulong>();
        NotifyCallback callback = (_, _, action, data, _) =>
        {
            if (action == ActionCustomEvent && Marshal.PtrToStructure<Guid>(data + 8) == RadioInRange)
            {
                var flags = (uint)Marshal.ReadInt32(data + EventDataOffset);
                if ((flags & (PairedFlag | VisibleFlag)) == (PairedFlag | VisibleFlag))
                {
                    lock (found)
                        found.Add((ulong)Marshal.ReadInt64(data + EventDataOffset + AddressOffset));
                }
            }
            return 0;
        };

        var radios = OpenRadios();
        var registrations = new List<IntPtr>();
        try
        {
            var filter = new byte[FilterSize];
            BitConverter.TryWriteBytes(filter.AsSpan(0), FilterSize);
            BitConverter.TryWriteBytes(filter.AsSpan(8), FilterTypeDeviceHandle);
            foreach (var radio in radios)
            {
                BitConverter.TryWriteBytes(filter.AsSpan(16), radio.ToInt64());
                if (CM_Register_Notification(filter, IntPtr.Zero, callback, out var registration) == 0)
                    registrations.Add(registration);
            }
            if (registrations.Count == 0)
                return found;

            Inquire();
            Thread.Sleep(500); // late notifications
        }
        finally
        {
            foreach (var registration in registrations)
                CM_Unregister_Notification(registration);
            foreach (var radio in radios)
                CloseHandle(radio);
            GC.KeepAlive(callback);
        }

        lock (found)
            return found.ToHashSet();
    }

    private static List<IntPtr> OpenRadios()
    {
        var radios = new List<IntPtr>();
        var findParams = new FindRadioParams { Size = (uint)Marshal.SizeOf<FindRadioParams>() };
        var find = BluetoothFindFirstRadio(ref findParams, out var radio);
        if (find == IntPtr.Zero)
            return radios;
        do
            radios.Add(radio);
        while (BluetoothFindNextRadio(find, out radio));
        BluetoothFindRadioClose(find);
        return radios;
    }

    /// <summary>Runs one inquiry on every radio; the results come through the notifications.</summary>
    private static void Inquire()
    {
        var search = new SearchParams
        {
            Size = (uint)Marshal.SizeOf<SearchParams>(),
            ReturnRemembered = 1, // as in the version tested on hardware
            ReturnUnknown = 1,
            IssueInquiry = 1,
            TimeoutMultiplier = InquiryLength,
        };
        var info = new DeviceInfo { Size = (uint)Marshal.SizeOf<DeviceInfo>() };
        var find = BluetoothFindFirstDevice(ref search, ref info);
        if (find != IntPtr.Zero)
            BluetoothFindDeviceClose(find);
    }

    private delegate uint NotifyCallback(IntPtr notify, IntPtr context, int action, IntPtr eventData, uint eventDataSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct FindRadioParams
    {
        public uint Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SearchParams
    {
        public uint Size;
        public int ReturnAuthenticated, ReturnRemembered, ReturnUnknown, ReturnConnected, IssueInquiry;
        public byte TimeoutMultiplier;
        public IntPtr Radio;
    }

    /// <summary>BLUETOOTH_DEVICE_INFO; only filled in, never read.</summary>
    [StructLayout(LayoutKind.Sequential, Size = 560)]
    private struct DeviceInfo
    {
        public uint Size;
    }

    [DllImport("bthprops.cpl", SetLastError = true)]
    private static extern IntPtr BluetoothFindFirstRadio(ref FindRadioParams findParams, out IntPtr radio);

    [DllImport("bthprops.cpl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothFindNextRadio(IntPtr find, out IntPtr radio);

    [DllImport("bthprops.cpl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothFindRadioClose(IntPtr find);

    [DllImport("bthprops.cpl", SetLastError = true)]
    private static extern IntPtr BluetoothFindFirstDevice(ref SearchParams search, ref DeviceInfo info);

    [DllImport("bthprops.cpl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothFindDeviceClose(IntPtr find);

    [DllImport("cfgmgr32.dll")]
    private static extern uint CM_Register_Notification(byte[] filter, IntPtr context, NotifyCallback callback, out IntPtr notify);

    [DllImport("cfgmgr32.dll")]
    private static extern uint CM_Unregister_Notification(IntPtr notify);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
