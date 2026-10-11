using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using WinSense.App.Views;

namespace WinSense.App;

public partial class MainWindow : Window
{
    // Pages are created on first visit and kept, so their state (e.g. the monitor history) survives navigation.
    private readonly Dictionary<string, Func<UserControl>> _pageFactories = new()
    {
        ["monitor"] = () => new MonitorView(),
        ["battery"] = () => new BatteryView(),
        ["bluetooth"] = () => new BluetoothView(),
        ["keyboard"] = () => new KeyboardView(),
        ["settings"] = () => new SettingsView(),
    };
    private readonly Dictionary<string, UserControl> _pages = [];

    public MainWindow()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        VersionText.Text = $"Versão {version}";

        Navigation.SelectedIndex = 0;
    }

    private void Navigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Navigation.SelectedItem is not ListBoxItem { Tag: string key })
            return;

        if (!_pages.TryGetValue(key, out var page))
            _pages[key] = page = _pageFactories[key]();
        PageHost.Content = page;
    }
}
