using System.Windows.Controls;
using WinSense.App.ViewModels;
using WinSense.Core.Battery;

namespace WinSense.App.Views;

public partial class BatteryView : UserControl
{
    public BatteryView()
    {
        InitializeComponent();

        var viewModel = new BatteryViewModel(BatteryChargeControllerFactory.Create(), BatteryInfo.Read);
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadAsync();
    }
}
