using WinSense.App;
using WinSense.App.ViewModels;
using WinSense.Core.Bluetooth;

namespace WinSense.Core.Tests;

public class BluetoothAddressTests
{
    [Fact]
    public void Format() => Assert.Equal("41:42:F3:F5:21:38", BluetoothAddress.Format(0x4142F3F52138));

    [Theory]
    [InlineData(0x002508u, true)]  // DualShock 4: peripheral, gamepad
    [InlineData(0x002504u, true)]  // joystick
    [InlineData(0x240404u, false)] // headphones (audio/video)
    [InlineData(0x002540u, false)] // keyboard
    [InlineData(0x002580u, false)] // mouse
    public void GameControllerByClassOfDevice(uint cod, bool expected) =>
        Assert.Equal(expected, BluetoothAddress.IsGameController(cod));

    [Theory]
    [InlineData(0x002508u, DeviceCategory.Input)]
    [InlineData(0x240404u, DeviceCategory.Audio)]
    [InlineData(0x5A020Cu, DeviceCategory.Other)] // phone
    public void CategoryByClassOfDevice(uint cod, DeviceCategory expected) =>
        Assert.Equal(expected, BluetoothAddress.Categorize(cod));
}

internal static class Devices
{
    public static PairedDevice Controller(ulong address, bool connected = false) =>
        new(address, "Wireless Controller", DeviceCategory.Input, ReconnectMethod.Hid, connected, IsGameController: true);

    public static PairedDevice Headphones(ulong address, bool connected = false) =>
        new(address, "H02D Pro Anc", DeviceCategory.Audio, ReconnectMethod.Audio, connected, IsGameController: false);

    public static PairedDevice Phone(ulong address) =>
        new(address, "Celular", DeviceCategory.Other, ReconnectMethod.None, IsConnected: false, IsGameController: false);
}

internal sealed class FakeBluetooth : IBluetoothDevices
{
    public List<PairedDevice> Paired { get; } = [];
    public List<ulong> Attempts { get; } = [];
    public ConnectResult Result { get; set; } = ConnectResult.Connected;
    public bool Throw { get; set; }

    public Task<IReadOnlyList<PairedDevice>> ListPairedAsync() =>
        Task.FromResult<IReadOnlyList<PairedDevice>>(Paired.ToList());

    public Task<ConnectResult> TryConnectAsync(PairedDevice device, CancellationToken cancellationToken)
    {
        Attempts.Add(device.Address);
        return Throw ? throw new InvalidOperationException("rádio ocupado") : Task.FromResult(Result);
    }
}

public class BluetoothAutoConnectorTests
{
    [Fact]
    public async Task TriesOnlyChosenDisconnectedDevices()
    {
        var bluetooth = new FakeBluetooth();
        bluetooth.Paired.Add(Devices.Controller(1, connected: true));
        bluetooth.Paired.Add(Devices.Controller(2));
        bluetooth.Paired.Add(Devices.Headphones(3));
        var attempts = new List<ConnectAttempt>();
        using var connector = new BluetoothAutoConnector(bluetooth, d => d.Address != 3);
        connector.Attempted += attempts.Add;

        await connector.RunOnceAsync(CancellationToken.None);

        Assert.Equal([2UL], bluetooth.Attempts);
        Assert.Equal(ConnectResult.Connected, Assert.Single(attempts).Result);
    }

    [Fact]
    public async Task NeverTriesDevicesWithoutAMethod()
    {
        var bluetooth = new FakeBluetooth();
        bluetooth.Paired.Add(Devices.Phone(4));
        using var connector = new BluetoothAutoConnector(bluetooth, _ => true);

        await connector.RunOnceAsync(CancellationToken.None);

        Assert.Empty(bluetooth.Attempts);
    }

    [Fact]
    public async Task Errors_AreReportedNotThrown()
    {
        var bluetooth = new FakeBluetooth { Throw = true };
        bluetooth.Paired.Add(Devices.Controller(2));
        var attempts = new List<ConnectAttempt>();
        using var connector = new BluetoothAutoConnector(bluetooth, _ => true);
        connector.Attempted += attempts.Add;

        await connector.RunOnceAsync(CancellationToken.None);

        var attempt = Assert.Single(attempts);
        Assert.Equal(ConnectResult.Failed, attempt.Result);
        Assert.Equal("rádio ocupado", attempt.Error);
    }

    [Fact]
    public async Task Cancelled_StopsBeforeTrying()
    {
        var bluetooth = new FakeBluetooth();
        bluetooth.Paired.Add(Devices.Controller(2));
        using var connector = new BluetoothAutoConnector(bluetooth, _ => true);

        await connector.RunOnceAsync(new CancellationToken(canceled: true));

        Assert.Empty(bluetooth.Attempts);
    }
}

public class AppSettingsTests
{
    [Fact]
    public void Default_ControllersOn_OthersOff()
    {
        var settings = new AppSettings();

        Assert.True(settings.ShouldAutoConnect(Devices.Controller(1)));
        Assert.False(settings.ShouldAutoConnect(Devices.Headphones(2)));
    }

    [Fact]
    public void Choice_OverridesDefault()
    {
        var settings = new AppSettings()
            .WithChoice(Devices.Controller(1), false)
            .WithChoice(Devices.Headphones(2), true);

        Assert.False(settings.ShouldAutoConnect(Devices.Controller(1)));
        Assert.True(settings.ShouldAutoConnect(Devices.Headphones(2)));
        Assert.True(settings.ShouldAutoConnect(Devices.Controller(3))); // untouched device keeps the default
    }
}

public class BluetoothViewModelTests
{
    private sealed class Harness : IDisposable
    {
        public FakeBluetooth Bluetooth { get; } = new();
        public AppSettings Settings { get; set; }
        public BluetoothAutoConnector Connector { get; }
        public BluetoothViewModel Vm { get; }

        public Harness(bool enabled)
        {
            Settings = new AppSettings(BluetoothAutoConnect: enabled);
            Bluetooth.Paired.Add(Devices.Controller(0x4142F3F52138));
            Bluetooth.Paired.Add(Devices.Headphones(0xF29DF42D2D63));
            Bluetooth.Paired.Add(Devices.Phone(0x1234));
            Connector = new BluetoothAutoConnector(Bluetooth, d => Settings.ShouldAutoConnect(d));
            Vm = new BluetoothViewModel(Bluetooth, Connector, () => Settings, s => Settings = s);
        }

        public void Dispose() => Connector.Dispose();
    }

    [Fact]
    public async Task Off_ShowsNothing()
    {
        using var h = new Harness(enabled: false);

        await h.Vm.LoadAsync();

        Assert.Empty(h.Vm.Groups);
    }

    [Fact]
    public async Task On_ShowsAllDevicesGroupedLikeSettings()
    {
        using var h = new Harness(enabled: true);

        await h.Vm.LoadAsync();

        Assert.Equal(["Entrada", "Áudio", "Outros dispositivos"], h.Vm.Groups.Select(g => g.Title));
        var controller = h.Vm.Groups[0].Devices.Single();
        Assert.Equal("41:42:F3:F5:21:38", controller.Address);
        Assert.Equal("Desconectado", controller.Status);
        Assert.True(controller.AutoConnect);              // controllers on by default
        Assert.False(h.Vm.Groups[1].Devices.Single().AutoConnect); // headphones off by default
        var phone = h.Vm.Groups[2].Devices.Single();
        Assert.False(phone.CanAutoConnect);
        Assert.NotEmpty(phone.Note);
    }

    [Fact]
    public async Task TurningOn_StartsAndLists_TurningOff_StopsAndHides()
    {
        using var h = new Harness(enabled: false);

        h.Vm.AutoConnect = true;
        await h.Vm.RefreshAsync();
        Assert.True(h.Connector.IsRunning);
        Assert.True(h.Settings.BluetoothAutoConnect);
        Assert.Equal(3, h.Vm.Groups.Count);

        h.Vm.AutoConnect = false;
        Assert.False(h.Connector.IsRunning);
        Assert.False(h.Settings.BluetoothAutoConnect);
        Assert.Empty(h.Vm.Groups);
        Assert.Empty(h.Vm.Log);
    }

    [Fact]
    public async Task CheckingADevice_SavesTheChoice_AndTheConnectorUsesIt()
    {
        using var h = new Harness(enabled: true);
        await h.Vm.LoadAsync();

        h.Vm.Groups[1].Devices.Single().AutoConnect = true; // headphones
        await h.Connector.RunOnceAsync(CancellationToken.None);

        Assert.True(h.Settings.AutoConnectChoices!["F2:9D:F4:2D:2D:63"]);
        Assert.Contains(0xF29DF42D2D63UL, h.Bluetooth.Attempts);
    }

    [Fact]
    public async Task Refresh_UpdatesStatusInPlace()
    {
        using var h = new Harness(enabled: true);
        await h.Vm.LoadAsync();
        var row = h.Vm.Groups[0].Devices.Single();

        h.Bluetooth.Paired[0] = Devices.Controller(0x4142F3F52138, connected: true);
        await h.Vm.RefreshAsync();

        Assert.Same(row, h.Vm.Groups[0].Devices.Single());
        Assert.Equal("Conectado", row.Status);
    }

    [Fact]
    public async Task Attempts_AppearInLog_NewestFirst()
    {
        using var h = new Harness(enabled: true);
        await h.Vm.LoadAsync();

        await h.Connector.RunOnceAsync(CancellationToken.None);

        Assert.Contains("Wireless Controller (41:42:F3:F5:21:38): conectado", h.Vm.Log[0]);
    }

    [Theory]
    [InlineData(ConnectResult.NotReachable, "não respondeu (desligado ou fora de alcance)")]
    [InlineData(ConnectResult.ConnectedAfterServiceRestart, "conectado (depois de reiniciar o serviço do dispositivo)")]
    public void Describe(ConnectResult result, string expected)
    {
        var attempt = new ConnectAttempt(new DateTime(2026, 10, 11, 14, 2, 5), Devices.Controller(2), result);

        Assert.Equal($"14:02:05  Wireless Controller (00:00:00:00:00:02): {expected}", BluetoothViewModel.Describe(attempt));
    }
}
