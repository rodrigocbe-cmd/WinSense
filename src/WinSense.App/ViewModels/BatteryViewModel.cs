using CommunityToolkit.Mvvm.ComponentModel;
using WinSense.Core.Battery;

namespace WinSense.App.ViewModels;

public partial class BatteryViewModel(IBatteryChargeController controller, Func<BatteryInfo?> readBatteryInfo)
    : ObservableObject
{
    private ChargeMode? _currentMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChangeMode))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private string _batteryStatusText = "";

    public string ProviderName => controller.ProviderName;

    public bool CanChangeMode => controller.IsSupported && _currentMode is not null && !IsBusy;

    public ChargeMode? CurrentMode => _currentMode;

    public bool IsOptimizedSelected
    {
        get => _currentMode == ChargeMode.Optimized;
        set { if (value) _ = ApplyAsync(ChargeMode.Optimized); }
    }

    public bool IsFullSelected
    {
        get => _currentMode == ChargeMode.Full;
        set { if (value) _ = ApplyAsync(ChargeMode.Full); }
    }

    public async Task LoadAsync()
    {
        RefreshBatteryStatus();

        if (!controller.IsSupported)
        {
            StatusMessage = "Recurso indisponível neste computador.";
            return;
        }

        await RunAsync(async () => SetCurrentMode(await Task.Run(controller.GetMode)));
    }

    public async Task ApplyAsync(ChargeMode mode)
    {
        if (mode == _currentMode || IsBusy)
            return;

        await RunAsync(async () =>
        {
            await Task.Run(() => controller.SetMode(mode));
            SetCurrentMode(mode);
            StatusMessage = mode == ChargeMode.Optimized
                ? "Carregamento otimizado ativado: a bateria vai parar em 80%."
                : "Carregamento completo ativado: a bateria vai carregar até 100%.";
        });

        // On failure, put the radio buttons back on the mode that is actually in effect.
        NotifySelectionChanged();
    }

    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Não foi possível concluir: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SetCurrentMode(ChargeMode mode)
    {
        _currentMode = mode;
        NotifySelectionChanged();
        OnPropertyChanged(nameof(CanChangeMode));
    }

    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(CurrentMode));
        OnPropertyChanged(nameof(IsOptimizedSelected));
        OnPropertyChanged(nameof(IsFullSelected));
    }

    private void RefreshBatteryStatus()
    {
        try
        {
            var info = readBatteryInfo();
            BatteryStatusText = info is null
                ? "Nenhuma bateria detectada."
                : $"Carga atual: {info.ChargePercent}% · {(info.IsPluggedIn ? "Na tomada" : "Na bateria")}";
        }
        catch (Exception)
        {
            BatteryStatusText = "";
        }
    }
}
