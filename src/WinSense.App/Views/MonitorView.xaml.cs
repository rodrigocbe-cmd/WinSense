using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WinSense.App.ViewModels;
using WinSense.Core.Monitoring;

namespace WinSense.App.Views;

public partial class MonitorView : UserControl
{
    private readonly MonitorViewModel _viewModel = new(() => new SystemMonitor());
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public MonitorView()
    {
        InitializeComponent();
        DataContext = _viewModel;

        _timer.Tick += async (_, _) => await _viewModel.RefreshAsync();
        // Only sample while the page is on screen.
        IsVisibleChanged += async (_, e) =>
        {
            if ((bool)e.NewValue)
            {
                await _viewModel.LoadAsync();
                _timer.Start();
            }
            else
            {
                _timer.Stop();
            }
        };
        Unloaded += (_, _) => _timer.Stop();
        Dispatcher.ShutdownStarted += (_, _) => _viewModel.Dispose();
    }
}
