using CommunityToolkit.Mvvm.ComponentModel;
using WinSense.Core.Elevation;

namespace WinSense.App.ViewModels;

public partial class SettingsViewModel(IStartWithWindows startWithWindows) : ObservableObject
{
    private bool _startWithWindows;
    private bool _loaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChangeStartWithWindows))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "";

    public bool CanChangeStartWithWindows => _loaded && startWithWindows.IsAvailable && !IsBusy;

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (value != _startWithWindows)
                _ = SetStartWithWindowsAsync(value);
        }
    }

    public async Task LoadAsync()
    {
        if (!startWithWindows.IsAvailable)
        {
            StatusMessage = "Disponível só na versão instalada do WinSense (a versão portátil não inicia com o Windows).";
            return;
        }

        try
        {
            _startWithWindows = await Task.Run(startWithWindows.IsEnabled);
            _loaded = true;
            OnPropertyChanged(nameof(StartWithWindows));
            OnPropertyChanged(nameof(CanChangeStartWithWindows));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Não foi possível ler a configuração: {ex.Message}";
        }
    }

    public async Task SetStartWithWindowsAsync(bool enabled)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            await Task.Run(() => startWithWindows.SetEnabled(enabled));
            _startWithWindows = enabled;
            StatusMessage = enabled
                ? "Pronto: o WinSense vai abrir com o Windows, nos ícones ocultos."
                : "Pronto: o WinSense não vai mais abrir com o Windows.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Não foi possível concluir: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            // Show what is actually in effect (unchanged on failure).
            OnPropertyChanged(nameof(StartWithWindows));
        }
    }
}
