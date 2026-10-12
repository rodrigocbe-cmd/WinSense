using System.Runtime.InteropServices;

namespace WinSense.Core.Monitoring.Native;

/// <summary>
/// Thin wrapper over the PDH API (the engine behind Task Manager and perfmon). Counters are added by
/// their English path, so it works on any Windows display language (category names are localized).
/// </summary>
internal sealed class PdhQuery : IDisposable
{
    private const uint PdhFmtDouble = 0x00000200;
    private const uint PdhFmtNoCap100 = 0x00008000;
    private const uint PdhMoreData = 0x800007D2;
    private const uint PdhCstatusValidData = 0x0;
    private const uint PdhCstatusNewData = 0x1;

    private IntPtr _query;

    public PdhQuery()
    {
        Check(PdhOpenQueryW(null, IntPtr.Zero, out _query), "PdhOpenQuery");
    }

    /// <summary>Adds a counter; returns <see cref="IntPtr.Zero"/> if it does not exist on this machine.</summary>
    public IntPtr TryAdd(string englishPath) =>
        PdhAddEnglishCounterW(_query, englishPath, IntPtr.Zero, out var counter) == 0 ? counter : IntPtr.Zero;

    /// <summary>Takes a sample. Rate counters (e.g. % usage) need two collections before they have a value.</summary>
    public void Collect() => PdhCollectQueryData(_query);

    public double? GetValue(IntPtr counter)
    {
        if (counter == IntPtr.Zero)
            return null;
        if (PdhGetFormattedCounterValue(counter, PdhFmtDouble | PdhFmtNoCap100, out _, out var value) != 0)
            return null;
        return IsValid(value.CStatus) ? value.DoubleValue : null;
    }

    /// <summary>Values of every instance of a wildcard counter, e.g. <c>\GPU Engine(*)\Utilization Percentage</c>.</summary>
    public IReadOnlyList<(string Instance, double Value)> GetArray(IntPtr counter)
    {
        if (counter == IntPtr.Zero)
            return [];

        uint size = 0;
        var status = PdhGetFormattedCounterArrayW(counter, PdhFmtDouble | PdhFmtNoCap100, ref size, out _, IntPtr.Zero);
        if (status != PdhMoreData || size == 0)
            return [];

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (PdhGetFormattedCounterArrayW(counter, PdhFmtDouble | PdhFmtNoCap100, ref size, out var count, buffer) != 0)
                return [];

            var items = new List<(string, double)>((int)count);
            var itemSize = Marshal.SizeOf<PdhFmtCounterValueItem>();
            for (var i = 0; i < count; i++)
            {
                var item = Marshal.PtrToStructure<PdhFmtCounterValueItem>(buffer + i * itemSize);
                if (IsValid(item.Value.CStatus))
                    items.Add((Marshal.PtrToStringUni(item.Name) ?? "", item.Value.DoubleValue));
            }
            return items;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void Dispose()
    {
        if (_query != IntPtr.Zero)
        {
            PdhCloseQuery(_query);
            _query = IntPtr.Zero;
        }
    }

    private static bool IsValid(uint status) => status is PdhCstatusValidData or PdhCstatusNewData;

    private static void Check(uint status, string call)
    {
        if (status != 0)
            throw new InvalidOperationException($"{call} falhou (0x{status:X8}).");
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PdhFmtCounterValue
    {
        [FieldOffset(0)] public uint CStatus;
        [FieldOffset(8)] public double DoubleValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PdhFmtCounterValueItem
    {
        public IntPtr Name;
        public PdhFmtCounterValue Value;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQueryW(string? dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounterW(IntPtr query, string fullCounterPath, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll")]
    private static extern uint PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint type, out PdhFmtCounterValue value);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhGetFormattedCounterArrayW(IntPtr counter, uint format, ref uint bufferSize, out uint itemCount, IntPtr itemBuffer);

    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);
}
