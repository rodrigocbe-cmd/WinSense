using System.Windows;
using System.Windows.Controls;
using WinSense.App.ViewModels;

namespace WinSense.App.Views;

public partial class BluetoothView : UserControl
{
    public BluetoothView()
    {
        InitializeComponent();

        var app = (App)Application.Current;
        var viewModel = new BluetoothViewModel(app.Bluetooth, app.BluetoothConnector, () => app.Settings, app.UpdateSettings);
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadAsync();
        Dispatcher.ShutdownStarted += (_, _) => viewModel.Dispose();
    }
}
