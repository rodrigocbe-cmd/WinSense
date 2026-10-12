using WinSense.App.ViewModels;
using WinSense.Core.Monitoring;

namespace WinSense.Core.Tests;

public class MonitorViewModelTests
{
    private const long Dgpu = 1;
    private const long Igpu = 2;

    private sealed class FakeMonitor : ISystemMonitor
    {
        public HardwareInventory Inventory { get; } = new(
            new SystemInfo("RODRIGO-LAPTOP", "Acer Nitro V 15 (Nitro ANV15-41)"),
            new CpuInfo("AMD Ryzen 7 7735HS with Radeon Graphics", 8, 16, 3200),
            new MemoryInfo(16UL << 30, "DDR5", 4800, 2),
            [
                new GpuInfo(Dgpu, "NVIDIA GeForce RTX 4050 Laptop GPU", GpuKind.Dedicated, 6UL << 30),
                new GpuInfo(Igpu, "AMD Radeon(TM) Graphics", GpuKind.Integrated, 512UL << 20),
            ]);

        public TemperatureReading Temperatures { get; set; } = new(65, null, null);
        public int Samples { get; private set; }
        public bool Disposed { get; private set; }

        public SystemSample Sample()
        {
            Samples++;
            return new SystemSample(42, 3.5, 16UL << 30, 4UL << 30,
                new Dictionary<long, GpuSample>
                {
                    [Dgpu] = new(0, 0),
                    [Igpu] = new(12, 256UL << 20),
                },
                Temperatures);
        }

        public void Dispose() => Disposed = true;
    }

    [Fact]
    public async Task Load_ShowsEachComponentWithItsName()
    {
        var vm = new MonitorViewModel(() => new FakeMonitor());

        await vm.LoadAsync();

        Assert.Equal(4, vm.Cards.Count);
        Assert.Equal("Processador (CPU)", vm.Cards[0].Kind);
        Assert.Equal("AMD Ryzen 7 7735HS with Radeon Graphics", vm.Cards[0].Name);
        Assert.Equal("Memória (RAM)", vm.Cards[1].Kind);
        Assert.Equal("16 GB DDR5", vm.Cards[1].Name);
        Assert.Equal("4800 MT/s · 2 pentes", vm.Cards[1].Details);
        Assert.Equal("Placa de vídeo dedicada (GPU)", vm.Cards[2].Kind);
        Assert.Equal("Placa de vídeo integrada (GPU)", vm.Cards[3].Kind);
    }

    [Fact]
    public async Task Load_ShowsComputerNameAndModel()
    {
        var vm = new MonitorViewModel(() => new FakeMonitor());

        await vm.LoadAsync();

        Assert.Equal("RODRIGO-LAPTOP", vm.ComputerName);
        Assert.Equal("Acer Nitro V 15 (Nitro ANV15-41)", vm.ComputerModel);
    }

    [Fact]
    public async Task Load_TakesFirstSample()
    {
        var vm = new MonitorViewModel(() => new FakeMonitor());

        await vm.LoadAsync();

        Assert.Equal("42%", vm.Cards[0].UsageText);
        Assert.Equal("Temperatura: 65 °C", vm.Cards[0].TemperatureText);
        Assert.Equal("25%", vm.Cards[1].UsageText);
        Assert.Equal("Em uso: 4,0 de 16,0 GB", vm.Cards[1].Info);
        Assert.Equal("12%", vm.Cards[3].UsageText);
    }

    [Fact]
    public async Task SleepingDiscreteGpu_IsExplained()
    {
        var vm = new MonitorViewModel(() => new FakeMonitor());

        await vm.LoadAsync();

        Assert.Equal("Temperatura: sem leitura (GPU em repouso)", vm.Cards[2].TemperatureText);
        Assert.Equal("Temperatura (a mesma da CPU): 65 °C", vm.Cards[3].TemperatureText);
    }

    [Fact]
    public async Task NoSensors_SaysUnavailable()
    {
        var vm = new MonitorViewModel(() => new FakeMonitor { Temperatures = TemperatureReading.None });

        await vm.LoadAsync();

        Assert.Equal("Temperatura: indisponível", vm.Cards[0].TemperatureText);
        Assert.Equal("Temperatura: indisponível", vm.Cards[2].TemperatureText);
        Assert.Equal("", vm.Cards[3].TemperatureText);
    }

    [Fact]
    public async Task History_KeepsTheLastValuesOnly()
    {
        var vm = new MonitorViewModel(() => new FakeMonitor());
        await vm.LoadAsync();

        for (var i = 0; i < ComponentCardViewModel.HistoryLength + 10; i++)
            await vm.RefreshAsync();

        Assert.Equal(ComponentCardViewModel.HistoryLength, vm.Cards[0].History.Count);
    }

    [Fact]
    public async Task MonitorFailure_IsReported()
    {
        var vm = new MonitorViewModel(() => throw new InvalidOperationException("sem WMI"));

        await vm.LoadAsync();

        Assert.Empty(vm.Cards);
        Assert.Contains("sem WMI", vm.StatusMessage);
    }

    [Fact]
    public async Task Dispose_ReleasesMonitor()
    {
        var monitor = new FakeMonitor();
        var vm = new MonitorViewModel(() => monitor);
        await vm.LoadAsync();

        vm.Dispose();

        Assert.True(monitor.Disposed);
    }
}
