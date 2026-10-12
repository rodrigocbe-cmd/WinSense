using System.Windows.Controls;
using WinSense.App.ViewModels;
using WinSense.Core.Keyboard;

namespace WinSense.App.Views;

public partial class KeyboardView : UserControl
{
    public KeyboardView()
    {
        InitializeComponent();

        var viewModel = new KeyboardViewModel(new NumLockAtStartup());
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadAsync();
    }
}
