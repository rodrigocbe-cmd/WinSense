using WinSense.App.ViewModels;
using WinSense.Core.Battery;

namespace WinSense.Core.Tests;

public class BatteryViewModelTests
{
    private sealed class FakeController(ChargeMode mode) : IBatteryChargeController
    {
        public ChargeMode Mode { get; private set; } = mode;
        public bool FailOnSet { get; init; }
        public int SetCalls { get; private set; }

        public bool IsSupported => true;
        public string ProviderName => "Fake";
        public ChargeMode GetMode() => Mode;

        public void SetMode(ChargeMode mode)
        {
            SetCalls++;
            if (FailOnSet)
                throw new InvalidOperationException("falhou");
            Mode = mode;
        }
    }

    private static BatteryInfo? NoBattery() => null;

    [Fact]
    public async Task Load_ReflectsModeReadFromFirmware()
    {
        var vm = new BatteryViewModel(new FakeController(ChargeMode.Optimized), NoBattery);

        await vm.LoadAsync();

        Assert.True(vm.IsOptimizedSelected);
        Assert.False(vm.IsFullSelected);
        Assert.True(vm.CanChangeMode);
    }

    [Fact]
    public async Task Apply_ChangesMode()
    {
        var controller = new FakeController(ChargeMode.Optimized);
        var vm = new BatteryViewModel(controller, NoBattery);
        await vm.LoadAsync();

        await vm.ApplyAsync(ChargeMode.Full);

        Assert.Equal(ChargeMode.Full, controller.Mode);
        Assert.True(vm.IsFullSelected);
    }

    [Fact]
    public async Task Apply_SameMode_DoesNotTouchFirmware()
    {
        var controller = new FakeController(ChargeMode.Full);
        var vm = new BatteryViewModel(controller, NoBattery);
        await vm.LoadAsync();

        await vm.ApplyAsync(ChargeMode.Full);

        Assert.Equal(0, controller.SetCalls);
    }

    [Fact]
    public async Task Apply_Failure_KeepsPreviousModeAndReportsError()
    {
        var controller = new FakeController(ChargeMode.Optimized) { FailOnSet = true };
        var vm = new BatteryViewModel(controller, NoBattery);
        await vm.LoadAsync();

        await vm.ApplyAsync(ChargeMode.Full);

        Assert.True(vm.IsOptimizedSelected);
        Assert.Contains("falhou", vm.StatusMessage);
    }

    [Fact]
    public async Task Unsupported_DisablesControls()
    {
        var vm = new BatteryViewModel(new UnsupportedBatteryChargeController("sem suporte"), NoBattery);

        await vm.LoadAsync();

        Assert.False(vm.CanChangeMode);
        Assert.Equal("sem suporte", vm.ProviderName);
    }
}
