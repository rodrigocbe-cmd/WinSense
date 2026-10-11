using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WinSense.Core.Bluetooth;

/// <summary>
/// Turns a paired device's Bluetooth HID service off and on again (the "Services" tab in Devices and
/// Printers). On re-enable Windows reinstalls the HID device and its driver connects to the controller.
/// Pairing is not touched. Requires administrator rights.
/// </summary>
internal static class HidService
{
    public const string PnpPrefix = @"BTHENUM\{00001124";

    private static readonly Guid HumanInterfaceDevice = new("00001124-0000-1000-8000-00805F9B34FB");
    private const uint ServiceDisable = 0;
    private const uint ServiceEnable = 1;

    public static void Restart(ulong address)
    {
        var findParams = new BluetoothFindRadioParams { Size = (uint)Marshal.SizeOf<BluetoothFindRadioParams>() };
        var find = BluetoothFindFirstRadio(ref findParams, out var radio);
        if (find == IntPtr.Zero)
            throw new InvalidOperationException("Nenhum rádio Bluetooth encontrado.");
        BluetoothFindRadioClose(find);

        try
        {
            var info = new BluetoothDeviceInfo { Size = (uint)Marshal.SizeOf<BluetoothDeviceInfo>(), Address = address };
            Check(BluetoothGetDeviceInfo(radio, ref info));

            var service = HumanInterfaceDevice;
            Check(BluetoothSetServiceState(radio, ref info, ref service, ServiceDisable));
            Thread.Sleep(500);

            // Once disabled it must come back, or the controller would stop working as a controller.
            uint error = 0;
            for (var attempt = 0; attempt < 5; attempt++)
            {
                error = BluetoothSetServiceState(radio, ref info, ref service, ServiceEnable);
                if (error == 0)
                    break;
                Thread.Sleep(1000);
            }
            Check(error);
        }
        finally
        {
            CloseHandle(radio);
        }
    }

    private static void Check(uint error)
    {
        if (error != 0)
            throw new Win32Exception((int)error);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BluetoothFindRadioParams
    {
        public uint Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemTime
    {
        public ushort Year, Month, DayOfWeek, Day, Hour, Minute, Second, Milliseconds;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct BluetoothDeviceInfo
    {
        public uint Size;
        public ulong Address;
        public uint ClassOfDevice;
        public int Connected;
        public int Remembered;
        public int Authenticated;
        public SystemTime LastSeen;
        public SystemTime LastUsed;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)]
        public string Name;
    }

    [DllImport("bthprops.cpl", SetLastError = true)]
    private static extern IntPtr BluetoothFindFirstRadio(ref BluetoothFindRadioParams findParams, out IntPtr radio);

    [DllImport("bthprops.cpl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothFindRadioClose(IntPtr find);

    [DllImport("bthprops.cpl")]
    private static extern uint BluetoothGetDeviceInfo(IntPtr radio, ref BluetoothDeviceInfo info);

    [DllImport("bthprops.cpl")]
    private static extern uint BluetoothSetServiceState(IntPtr radio, ref BluetoothDeviceInfo info, ref Guid service, uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
