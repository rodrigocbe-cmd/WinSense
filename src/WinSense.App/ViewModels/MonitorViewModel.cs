using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using WinSense.Core.Monitoring;

namespace WinSense.App.ViewModels;

public partial class MonitorViewModel(Func<ISystemMonitor> createMonitor) : ObservableObject, IDisposable
{
    private const double BytesPerGb = 1024.0 * 1024 * 1024;
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");

    private ISystemMonitor? _monitor;
    private ComponentCardViewModel? _cpu;
    private ComponentCardViewModel? _memory;
    private readonly List<(GpuInfo Info, ComponentCardViewModel Card)> _gpus = [];
    private bool _refreshing;

    public ObservableCollection<ComponentCardViewModel> Cards { get; } = [];

    [ObservableProperty]
    private string _statusMessage = "Lendo o hardware...";

    [ObservableProperty]
    private string _computerName = "";

    [ObservableProperty]
    private string _computerModel = "";

    public async Task LoadAsync()
    {
        if (_monitor is not null)
            return;

        try
        {
            _monitor = await Task.Run(createMonitor);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Não foi possível ler o hardware: {ex.Message}";
            return;
        }

        var inventory = _monitor.Inventory;
        ComputerName = inventory.System.ComputerName;
        ComputerModel = inventory.System.Model ?? "";

        var cpu = inventory.Cpu;
        _cpu = new ComponentCardViewModel("Processador (CPU)", cpu.Name,
            string.Format(Pt, "{0} núcleos · {1} threads · base de {2:0.0#} GHz", cpu.Cores, cpu.Threads, cpu.BaseClockMhz / 1000.0));
        Cards.Add(_cpu);

        var memory = inventory.Memory;
        _memory = new ComponentCardViewModel("Memória (RAM)", MemoryName(memory), MemoryDetails(memory));
        Cards.Add(_memory);

        foreach (var gpu in inventory.Gpus)
        {
            var card = new ComponentCardViewModel(
                gpu.Kind == GpuKind.Dedicated ? "Placa de vídeo dedicada (GPU)" : "Placa de vídeo integrada (GPU)",
                gpu.Name,
                gpu.Kind == GpuKind.Dedicated
                    ? string.Format(Pt, "{0:0.#} GB de memória de vídeo própria", gpu.DedicatedMemoryBytes / BytesPerGb)
                    : "Fica no mesmo chip da CPU e usa a memória RAM");
            _gpus.Add((gpu, card));
            Cards.Add(card);
        }

        StatusMessage = "";
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (_monitor is null || _refreshing)
            return;

        _refreshing = true;
        try
        {
            Apply(await Task.Run(_monitor.Sample));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Falha ao atualizar: {ex.Message}";
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void Apply(SystemSample sample)
    {
        var temps = sample.Temperatures;

        if (_cpu is not null)
        {
            _cpu.Push(sample.CpuUsagePercent ?? 0);
            _cpu.Info = sample.CpuClockGhz is { } ghz ? string.Format(Pt, "Velocidade atual: {0:0.00} GHz", ghz) : "";
            _cpu.TemperatureText = temps.CpuCelsius is { } c
                ? FormatTemperature("Temperatura", c)
                : temps.SystemCelsius is { } s ? FormatTemperature("Temperatura do sistema (ACPI)", s) : "Temperatura: indisponível";
        }

        if (_memory is not null && sample.MemoryTotalBytes > 0)
        {
            _memory.Push(100.0 * sample.MemoryUsedBytes / sample.MemoryTotalBytes);
            _memory.Info = string.Format(Pt, "Em uso: {0:0.0} de {1:0.0} GB",
                sample.MemoryUsedBytes / BytesPerGb, sample.MemoryTotalBytes / BytesPerGb);
        }

        foreach (var (info, card) in _gpus)
        {
            var gpu = sample.GpusByLuid.GetValueOrDefault(info.Luid);
            card.Push(gpu?.UsagePercent ?? 0);

            if (info.Kind == GpuKind.Dedicated)
            {
                card.Info = gpu?.DedicatedMemoryUsedBytes is { } used
                    ? string.Format(Pt, "Memória de vídeo em uso: {0:0.0} de {1:0.0} GB", used / BytesPerGb, info.DedicatedMemoryBytes / BytesPerGb)
                    : "";
                // Laptop GPUs power off when idle; the sensor then reads nothing.
                card.TemperatureText = temps.GpuCelsius is { } g
                    ? FormatTemperature("Temperatura", g)
                    : temps.CpuCelsius is not null ? "Temperatura: sem leitura (GPU em repouso)" : "Temperatura: indisponível";
            }
            else
            {
                card.Info = gpu?.DedicatedMemoryUsedBytes is { } used
                    ? string.Format(Pt, "Memória reservada em uso: {0:0.0} GB", used / BytesPerGb)
                    : "";
                card.TemperatureText = temps.CpuCelsius is { } c ? FormatTemperature("Temperatura (a mesma da CPU)", c) : "";
            }
        }
    }

    private static string FormatTemperature(string label, double celsius) =>
        string.Format(Pt, "{0}: {1:0} °C", label, celsius);

    private static string MemoryName(MemoryInfo memory)
    {
        var size = string.Format(Pt, "{0:0.#} GB", memory.InstalledBytes / BytesPerGb);
        return memory.Type is null ? size : $"{size} {memory.Type}";
    }

    private static string MemoryDetails(MemoryInfo memory)
    {
        var parts = new List<string>();
        if (memory.SpeedMts is { } speed)
            parts.Add($"{speed} MT/s");
        if (memory.Modules > 0)
            parts.Add(memory.Modules == 1 ? "1 pente" : $"{memory.Modules} pentes");
        return string.Join(" · ", parts);
    }

    public void Dispose()
    {
        _monitor?.Dispose();
        _monitor = null;
    }
}
