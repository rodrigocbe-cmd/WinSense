using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinSense.Core.Keyboard;

namespace WinSense.App.ViewModels;

public partial class KeyboardViewModel(INumLockAtStartup numLock) : ObservableObject
{
    private NumLockState? _numLock;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChangeNumLock))]
    private bool _isBusy;

    [ObservableProperty]
    private string _numLockStatus = "";

    public bool CanChangeNumLock => _numLock is not null && !IsBusy;

    /// <summary>The lock screen starts differently from the user session; offers to make them match.</summary>
    public bool IsNumLockMixed => _numLock?.IsMixed == true;

    // The selection shows the session value: it is what the user sees every day after signing in.
    public bool IsNumLockOnSelected
    {
        get => _numLock?.Session == true;
        set { if (value) _ = SetNumLockAsync(true); }
    }

    public bool IsNumLockOffSelected
    {
        get => _numLock?.Session == false;
        set { if (value) _ = SetNumLockAsync(false); }
    }

    public async Task LoadAsync()
    {
        try
        {
            SetNumLock(await Task.Run(numLock.Read));
        }
        catch (Exception ex)
        {
            NumLockStatus = $"Não foi possível ler a configuração: {ex.Message}";
        }
    }

    public async Task SetNumLockAsync(bool enabled)
    {
        if (_numLock == new NumLockState(enabled, enabled) || IsBusy)
            return;

        IsBusy = true;
        try
        {
            await Task.Run(() => numLock.SetEnabled(enabled));
            SetNumLock(new NumLockState(enabled, enabled));
            NumLockStatus = enabled
                ? "Pronto: o Num Lock vai estar ligado na próxima vez que o PC ligar."
                : "Pronto: o Num Lock vai estar desligado na próxima vez que o PC ligar.";
        }
        catch (Exception ex)
        {
            NumLockStatus = $"Não foi possível concluir: {ex.Message}";
            // Put the radio buttons back on what is actually in effect.
            NotifyNumLockChanged();
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Applies the session's choice to the lock screen too.</summary>
    [RelayCommand]
    private Task MatchLockScreen() => _numLock is { } state ? SetNumLockAsync(state.Session) : Task.CompletedTask;

    private void SetNumLock(NumLockState state)
    {
        _numLock = state;
        if (state.IsMixed)
            NumLockStatus = state.LockScreen
                ? "Atenção: na tela de bloqueio o Num Lock ainda começa ligado."
                : "Atenção: na tela de bloqueio o Num Lock ainda começa desligado.";
        NotifyNumLockChanged();
        OnPropertyChanged(nameof(CanChangeNumLock));
    }

    private void NotifyNumLockChanged()
    {
        OnPropertyChanged(nameof(IsNumLockOnSelected));
        OnPropertyChanged(nameof(IsNumLockOffSelected));
        OnPropertyChanged(nameof(IsNumLockMixed));
    }
}
