using System.Runtime.InteropServices;

namespace WinSense.Core.Bluetooth;

/// <summary>Device tree queries through the Configuration Manager API (what Device Manager uses).</summary>
internal static class DeviceNodes
{
    private const uint CrSuccess = 0;
    private const uint LocateNormal = 0; // present devices only

    /// <summary>True if the device is present and has at least one present child device.</summary>
    public static bool HasPresentChild(string deviceInstanceId) =>
        CM_Locate_DevNodeW(out var node, deviceInstanceId, LocateNormal) == CrSuccess
        && CM_Get_Child(out _, node, 0) == CrSuccess;

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Locate_DevNodeW(out uint devInst, string deviceId, uint flags);

    [DllImport("cfgmgr32.dll")]
    private static extern uint CM_Get_Child(out uint child, uint devInst, uint flags);
}
