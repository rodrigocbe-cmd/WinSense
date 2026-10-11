using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinSense.Core.Bluetooth;

namespace WinSense.App.ViewModels;

/// <summary>One paired device, with its own "connect automatically" checkbox.</summary>
public partial class DeviceRowViewModel(PairedDevice device, bool autoConnect, Action<PairedDevice, bool> choose)
    : ObservableObject
{
    private bool _autoConnect = autoConnect;

    public PairedDevice Device { get; private set; } = device;

    public string Name => Device.Name;

    public string Address => Device.AddressText;

    public bool CanAutoConnect => Device.CanAutoConnect;

    public string Status => Device.IsConnected ? "Conectado" : "Desconectado";

    public string Note => CanAutoConnect ? "" : "Conexão automática não disponível para este tipo de dispositivo.";

    public bool AutoConnect
    {
        get => _autoConnect;
        set
        {
            if (SetProperty(ref _autoConnect, value))
                choose(Device, value);
        }
    }

    public void Update(PairedDevice device)
    {
        Device = device;
        OnPropertyChanged(nameof(Status));
    }
}

public sealed record DeviceGroup(string Title, ObservableCollection<DeviceRowViewModel> Devices);

public partial class BluetoothViewModel : ObservableObject, IDisposable
{
    private const int MaxLogEntries = 30;

    private readonly IBluetoothDevices _bluetooth;
    private readonly BluetoothAutoConnector _connector;
    private readonly Func<AppSettings> _settings;
    private readonly Action<AppSettings> _saveSettings;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;

    /// <param name="settings">Current settings (the app's, or a stub in tests).</param>
    /// <param name="saveSettings">Persists changed settings.</param>
    public BluetoothViewModel(IBluetoothDevices bluetooth, BluetoothAutoConnector connector,
        Func<AppSettings> settings, Action<AppSettings> saveSettings)
    {
        _bluetooth = bluetooth;
        _connector = connector;
        _settings = settings;
        _saveSettings = saveSettings;
        _connector.Attempted += OnAttempted;
        _connector.DevicesRefreshed += OnDevicesRefreshed;
    }

    /// <summary>Paired devices grouped like Windows Settings (Entrada, Áudio, Outros). Empty while the switch is off.</summary>
    public ObservableCollection<DeviceGroup> Groups { get; } = [];

    /// <summary>Latest attempts first.</summary>
    public ObservableCollection<string> Log { get; } = [];

    [ObservableProperty]
    private string _statusMessage = "";

    public bool AutoConnect
    {
        get => _settings().BluetoothAutoConnect;
        set
        {
            if (value == AutoConnect)
                return;

            Save(_settings() with { BluetoothAutoConnect = value });
            OnPropertyChanged();

            if (value)
            {
                _connector.Start();
                _ = RefreshAsync();
            }
            else
            {
                _connector.Stop();
                Groups.Clear();
                Log.Clear();
                StatusMessage = "";
            }
        }
    }

    public async Task LoadAsync()
    {
        if (AutoConnect)
            await RefreshAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        try
        {
            var devices = await _bluetooth.ListPairedAsync();
            if (AutoConnect)
                ShowDevices(devices);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Não foi possível ler os dispositivos Bluetooth: {ex.Message}";
        }
    }

    private void ShowDevices(IReadOnlyList<PairedDevice> devices)
    {
        StatusMessage = devices.Count == 0
            ? "Nenhum dispositivo pareado. Pareie uma vez em Configurações do Windows › Bluetooth e dispositivos."
            : "";

        var rows = Groups.SelectMany(g => g.Devices).ToDictionary(r => r.Device.Address);
        var sameDevices = rows.Count == devices.Count && devices.All(d => rows.ContainsKey(d.Address));
        if (sameDevices)
        {
            // Only statuses changed: update in place so checkboxes do not flicker under the mouse.
            foreach (var device in devices)
                rows[device.Address].Update(device);
            return;
        }

        Groups.Clear();
        var settings = _settings();
        foreach (var group in devices.GroupBy(d => d.Category).OrderBy(g => g.Key))
        {
            var groupRows = group.Select(d => new DeviceRowViewModel(d, settings.ShouldAutoConnect(d), Choose));
            Groups.Add(new DeviceGroup(Title(group.Key), new ObservableCollection<DeviceRowViewModel>(groupRows)));
        }
    }

    private void Choose(PairedDevice device, bool enabled) => Save(_settings().WithChoice(device, enabled));

    private void Save(AppSettings settings)
    {
        try
        {
            _saveSettings(settings);
        }
        catch (Exception ex)
        {
            StatusMessage = $"A opção vale agora, mas não foi salva: {ex.Message}";
        }
    }

    private void OnDevicesRefreshed(IReadOnlyList<PairedDevice> devices) => OnUi(() =>
    {
        if (AutoConnect)
            ShowDevices(devices);
    });

    private void OnAttempted(ConnectAttempt attempt) => OnUi(() =>
    {
        if (!AutoConnect)
            return;
        Log.Insert(0, Describe(attempt));
        while (Log.Count > MaxLogEntries)
            Log.RemoveAt(Log.Count - 1);
    });

    private static string Title(DeviceCategory category) => category switch
    {
        DeviceCategory.Input => "Entrada",
        DeviceCategory.Audio => "Áudio",
        _ => "Outros dispositivos",
    };

    public static string Describe(ConnectAttempt attempt)
    {
        var time = attempt.Time.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        var text = attempt.Result switch
        {
            ConnectResult.Connected => "conectado",
            ConnectResult.ConnectedAfterServiceRestart => "conectado (depois de reiniciar o serviço do dispositivo)",
            ConnectResult.NotReachable => "não respondeu (desligado ou fora de alcance)",
            _ => attempt.Error is null ? "respondeu, mas não conectou" : $"falhou: {attempt.Error}",
        };
        return $"{time}  {attempt.Device.Name} ({attempt.Device.AddressText}): {text}";
    }

    private void OnUi(Action action)
    {
        // The connector raises its events from a background thread; only those need to hop to the UI.
        if (_ui is null || SynchronizationContext.Current == _ui)
            action();
        else
            _ui.Post(_ => action(), null);
    }

    public void Dispose()
    {
        _connector.Attempted -= OnAttempted;
        _connector.DevicesRefreshed -= OnDevicesRefreshed;
    }
}
