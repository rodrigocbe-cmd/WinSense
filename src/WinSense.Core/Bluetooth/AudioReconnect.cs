using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace WinSense.Core.Bluetooth;

/// <summary>
/// Asks the Bluetooth audio (A2DP) driver to reconnect a paired headset or speaker: the
/// KSPROPERTY_ONESHOT_RECONNECT request that Windows Settings sends when you click "Connect".
/// The driver's audio filter stays present while the device is disconnected, and its interface
/// path contains the device address. Requires administrator rights to open the filter.
/// </summary>
internal static class AudioReconnect
{
    /// <summary>KSCATEGORY_AUDIO.</summary>
    private static readonly Guid AudioInterfaceClass = new("6994AD04-93EF-11D0-A3CC-00A0C9223196");

    /// <summary>KSPROPSETID_BtAudio; property 0 = KSPROPERTY_ONESHOT_RECONNECT.</summary>
    private static readonly Guid BtAudioPropertySet = new("7FA06C40-B8F6-4C7E-8556-E8C33A12E54D");
    private const uint OneShotReconnect = 0;
    private const uint KsPropertyTypeGet = 0x1;

    /// <summary>CTL_CODE(FILE_DEVICE_KS, 0, METHOD_NEITHER, FILE_ANY_ACCESS).</summary>
    private const uint IoctlKsProperty = 0x002F0003;

    private const string A2dpService = "{0000110b-0000-1000-8000-00805f9b34fb}";

    /// <summary>Interface paths of present A2DP audio filters (one per paired headset/speaker).</summary>
    public static IReadOnlyList<string> FilterPaths()
    {
        var guid = AudioInterfaceClass;
        if (CM_Get_Device_Interface_List_SizeW(out var length, ref guid, null, 0) != 0 || length <= 1)
            return [];

        var buffer = new char[length];
        if (CM_Get_Device_Interface_ListW(ref guid, null, buffer, length, 0) != 0)
            return [];

        return new string(buffer)
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(path => path.Contains(A2dpService, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>Sends the reconnect request. False if the device has no audio filter or the driver refused.</summary>
    public static bool Request(ulong address)
    {
        var hex = address.ToString("X12");
        var path = FilterPaths().FirstOrDefault(p => p.Contains(hex, StringComparison.OrdinalIgnoreCase));
        if (path is null)
            return false;

        using var handle = CreateFileW(path, GenericRead | GenericWrite, FileShareRead | FileShareWrite,
            IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        if (handle.IsInvalid)
            return false;

        var property = new KsProperty { Set = BtAudioPropertySet, Id = OneShotReconnect, Flags = KsPropertyTypeGet };
        return DeviceIoControl(handle, IoctlKsProperty, ref property, (uint)Marshal.SizeOf<KsProperty>(),
            IntPtr.Zero, 0, out _, IntPtr.Zero);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KsProperty
    {
        public Guid Set;
        public uint Id;
        public uint Flags;
    }

    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x1;
    private const uint FileShareWrite = 0x2;
    private const uint OpenExisting = 3;

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_Device_Interface_List_SizeW(out int length, ref Guid interfaceClass, string? deviceId, int flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_Device_Interface_ListW(ref Guid interfaceClass, string? deviceId, char[] buffer, int length, int flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string fileName, uint access, uint share, IntPtr security,
        uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle device, uint ioControlCode, ref KsProperty input, uint inputSize,
        IntPtr output, uint outputSize, out uint bytesReturned, IntPtr overlapped);
}
