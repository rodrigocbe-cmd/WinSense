using System.Windows;
using System.Windows.Controls;
using WinSense.App.ViewModels;

namespace WinSense.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();

        var viewModel = new SettingsViewModel(((App)Application.Current).StartWithWindows);
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadAsync();
    }
}
