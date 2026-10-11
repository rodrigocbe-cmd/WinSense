using System.Runtime.InteropServices;

namespace WinSense.Core.Monitoring.Native;

/// <summary>
/// Lists display adapters through DXGI. Unlike Win32_VideoController it reports VRAM above 4 GB and
/// the adapter LUID, which is how the "GPU Engine" performance counters identify each GPU.
/// Enumerating adapters does not create a device, so it does not wake a sleeping discrete GPU.
/// </summary>
internal static class Dxgi
{
    private const int DxgiErrorNotFound = unchecked((int)0x887A0002);
    private const uint DxgiAdapterFlagSoftware = 2;

    public sealed record Adapter(long Luid, string Description, uint VendorId, ulong DedicatedVideoMemory);

    public static IReadOnlyList<Adapter> EnumerateHardwareAdapters()
    {
        var iid = typeof(IDXGIFactory1).GUID;
        if (CreateDXGIFactory1(ref iid, out var factoryObject) != 0)
            return [];

        var factory = (IDXGIFactory1)factoryObject;
        var adapters = new List<Adapter>();
        try
        {
            for (uint i = 0; factory.EnumAdapters1(i, out var adapter) != DxgiErrorNotFound; i++)
            {
                try
                {
                    if (adapter.GetDesc1(out var desc) != 0 || (desc.Flags & DxgiAdapterFlagSoftware) != 0)
                        continue;

                    var luid = ((long)desc.AdapterLuidHighPart << 32) | desc.AdapterLuidLowPart;
                    // The same physical GPU can appear once per output; keep the first.
                    if (adapters.All(a => a.Luid != luid))
                        adapters.Add(new Adapter(luid, desc.Description.Trim(), desc.VendorId, desc.DedicatedVideoMemory));
                }
                finally
                {
                    Marshal.ReleaseComObject(adapter);
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(factory);
        }
        return adapters;
    }

    [DllImport("dxgi.dll")]
    private static extern int CreateDXGIFactory1(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object factory);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DxgiAdapterDesc1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public nuint DedicatedVideoMemoryRaw;
        public nuint DedicatedSystemMemory;
        public nuint SharedSystemMemory;
        public uint AdapterLuidLowPart;
        public int AdapterLuidHighPart;
        public uint Flags;

        public readonly ulong DedicatedVideoMemory => DedicatedVideoMemoryRaw;
    }

    // Only the slots we call are declared with real signatures; the others just hold their vtable position.
    [ComImport, Guid("770aae78-f26f-4dba-a829-253c83d1b387"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIFactory1
    {
        void SetPrivateData();
        void SetPrivateDataInterface();
        void GetPrivateData();
        void GetParent();
        void EnumAdapters();
        void MakeWindowAssociation();
        void GetWindowAssociation();
        void CreateSwapChain();
        void CreateSoftwareAdapter();
        [PreserveSig] int EnumAdapters1(uint index, out IDXGIAdapter1 adapter);
        [PreserveSig] int IsCurrent();
    }

    [ComImport, Guid("29038f61-3839-4626-91fd-086879011a05"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIAdapter1
    {
        void SetPrivateData();
        void SetPrivateDataInterface();
        void GetPrivateData();
        void GetParent();
        void EnumOutputs();
        void GetDesc();
        void CheckInterfaceSupport();
        [PreserveSig] int GetDesc1(out DxgiAdapterDesc1 desc);
    }
}
