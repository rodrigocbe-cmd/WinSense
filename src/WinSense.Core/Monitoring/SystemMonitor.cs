using WinSense.Core.Monitoring.Native;
using WinSense.Core.Monitoring.Temperature;

namespace WinSense.Core.Monitoring;

public interface ISystemMonitor : IDisposable
{
    HardwareInventory Inventory { get; }

    /// <summary>Reads the live values. Usage is averaged since the previous call, so call it at a steady pace.</summary>
    SystemSample Sample();
}

/// <summary>
/// Live CPU, RAM and GPU usage from the same performance counters Task Manager uses, plus temperatures
/// from the best vendor source available. Construction is slow (WMI + DXGI): build it off the UI thread.
/// </summary>
public sealed class SystemMonitor : ISystemMonitor
{
    private readonly PdhQuery _pdh = new();
    private readonly IntPtr _cpuUtility;
    private readonly IntPtr _cpuPerformance;
    private readonly IntPtr _gpuEngines;
    private readonly IntPtr _gpuMemory;
    private readonly ITemperatureSource _temperatures;

    public SystemMonitor()
    {
        Inventory = HardwareInventoryReader.Read();
        _temperatures = TemperatureSourceFactory.Create();

        // "% Processor Utility" matches Task Manager (it accounts for turbo); fall back to the classic counter.
        _cpuUtility = _pdh.TryAdd(@"\Processor Information(_Total)\% Processor Utility");
        if (_cpuUtility == IntPtr.Zero)
            _cpuUtility = _pdh.TryAdd(@"\Processor(_Total)\% Processor Time");
        _cpuPerformance = _pdh.TryAdd(@"\Processor Information(_Total)\% Processor Performance");
        _gpuEngines = _pdh.TryAdd(@"\GPU Engine(*)\Utilization Percentage");
        _gpuMemory = _pdh.TryAdd(@"\GPU Adapter Memory(*)\Dedicated Usage");

        // Rate counters need a baseline sample.
        _pdh.Collect();
    }

    public HardwareInventory Inventory { get; }

    public SystemSample Sample()
    {
        _pdh.Collect();

        var cpu = _pdh.GetValue(_cpuUtility) is { } u ? Math.Clamp(u, 0, 100) : (double?)null;
        double? clock = _pdh.GetValue(_cpuPerformance) is { } perf && Inventory.Cpu.BaseClockMhz > 0
            ? Inventory.Cpu.BaseClockMhz * perf / 100 / 1000
            : null;

        var (total, available) = MemoryStatus.Read();

        var usage = GpuCounters.UsageByLuid(_pdh.GetArray(_gpuEngines));
        var memory = GpuCounters.MemoryByLuid(_pdh.GetArray(_gpuMemory));
        // A GPU in deep sleep publishes no counters: that is 0% usage, not "unknown".
        var gpus = Inventory.Gpus.ToDictionary(
            g => g.Luid,
            g => new GpuSample(usage.GetValueOrDefault(g.Luid), memory.TryGetValue(g.Luid, out var m) ? m : null));

        TemperatureReading temperatures;
        try
        {
            temperatures = _temperatures.Read();
        }
        catch (Exception)
        {
            temperatures = TemperatureReading.None;
        }

        return new SystemSample(cpu, clock, total, total - available, gpus, temperatures);
    }

    public void Dispose()
    {
        _pdh.Dispose();
        (_temperatures as IDisposable)?.Dispose();
    }
}
