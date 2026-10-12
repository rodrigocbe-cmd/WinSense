using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using WinSense.Core.Bluetooth;
using WinSense.Core.Elevation;

namespace WinSense.App;

public partial class App : Application
{
    private const int ErrorCancelled = 1223;

    // Used by the installer (elevated) to turn "start with Windows" on or off, then exit.
    private const string RegisterStartupArgument = "--register-startup";
    private const string UnregisterStartupArgument = "--unregister-startup";

    // WINSENSE_NO_ELEVATE=1: development only, to look at the UI without the UAC prompt.
    private static readonly bool DevelopmentMode = Environment.GetEnvironmentVariable("WINSENSE_NO_ELEVATE") == "1";

    // Per-session single instance. Only elevated copies get this far, so they can all open these objects.
    // The development copy uses its own names so it can run next to an installed WinSense.
    private static readonly string InstanceMutexName = @"Local\WinSense.Instance" + (DevelopmentMode ? ".Dev" : "");
    private static readonly string ShowWindowEventName = @"Local\WinSense.ShowWindow" + (DevelopmentMode ? ".Dev" : "");

    private Mutex? _instanceMutex;
    private EventWaitHandle? _showWindowEvent;
    private TrayIcon? _tray;
    private MainWindow? _window;
    private bool _exiting;

    public AppSettings Settings { get; private set; } = AppSettings.Load();

    public IBluetoothDevices Bluetooth { get; } = new WindowsBluetoothDevices();

    /// <summary>Lives with the app, not the page, so the option works without opening "Bluetooth e Dispositivos".</summary>
    public BluetoothAutoConnector BluetoothConnector { get; private set; } = null!;

    public IStartWithWindows StartWithWindows { get; private set; } = null!;

    public void UpdateSettings(AppSettings settings)
    {
        Settings = settings;
        settings.Save();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // The exe starts unelevated (asInvoker) and relaunches itself as administrator: through the
        // scheduled task when installed (no UAC prompt), otherwise with the regular UAC prompt.
        if (!ElevatedStartupTask.IsElevated && !DevelopmentMode)
        {
            StartElevatedCopy(e.Args);
            Shutdown();
            return;
        }

        var exePath = Environment.ProcessPath ?? "";
        StartWithWindows = new StartWithWindows(exePath);

        if (e.Args.Contains(RegisterStartupArgument) || e.Args.Contains(UnregisterStartupArgument))
        {
            RunInstallerCommand(exePath, enable: e.Args.Contains(RegisterStartupArgument));
            Shutdown();
            return;
        }

        var background = e.Args.Contains(Core.Elevation.StartWithWindows.BackgroundArgument);
        _instanceMutex = new Mutex(true, InstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            // Already running (maybe hidden in the tray): bring that window up instead.
            if (!background && EventWaitHandle.TryOpenExisting(ShowWindowEventName, out var showWindow))
                using (showWindow)
                    showWindow.Set();
            Shutdown();
            return;
        }

        // Create or repair the no-prompt task for next time. Off the UI thread: it spawns schtasks.
        _ = Task.Run(() =>
        {
            try { ElevatedStartupTask.EnsureCreated(exePath); }
            catch (Exception) { /* Next start just shows the UAC prompt. */ }
        });

        // Reads Settings on every round, so checkbox changes apply without restarting it.
        BluetoothConnector = new BluetoothAutoConnector(Bluetooth, device => Settings.ShouldAutoConnect(device),
            device => Settings.ShouldCall(device));
        if (Settings.BluetoothAutoConnect)
            BluetoothConnector.Start();

        _window = new MainWindow();
        WindowPlacement.Restore(_window);
        WindowPlacement.TrackAndSaveOnClose(_window);
        MainWindow = _window;
        // The X button hides WinSense in the notification area; "Sair" in the tray menu quits.
        _window.Closing += (_, args) =>
        {
            if (_exiting)
                return;
            args.Cancel = true;
            _window.Hide();
            _tray?.ShowStillRunningHint();
        };

        _tray = new TrayIcon(ShowMainWindow, ExitApp);
        ListenForShowRequests();

        if (!background)
            ShowMainWindow();
    }

    /// <summary>Windows is logging off or shutting down: let the window close for real.</summary>
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _exiting = true;
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        BluetoothConnector?.Dispose();
        _showWindowEvent?.Dispose();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }

    private void ShowMainWindow()
    {
        if (_window is null)
            return;

        _window.Show();
        if (_window.WindowState == WindowState.Minimized)
            _window.WindowState = WindowState.Normal;

        // Bring it to the front even when asked by another process.
        _window.Topmost = true;
        _window.Topmost = false;
        _window.Activate();
    }

    private void ExitApp()
    {
        _exiting = true;
        _window?.Close();
        Shutdown();
    }

    private void ListenForShowRequests()
    {
        _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowEventName);
        var thread = new Thread(() =>
        {
            try
            {
                while (_showWindowEvent.WaitOne())
                    Dispatcher.BeginInvoke(ShowMainWindow);
            }
            catch (ObjectDisposedException)
            {
                // App is exiting.
            }
        })
        { IsBackground = true, Name = "WinSense show-window listener" };
        thread.Start();
    }

    private void RunInstallerCommand(string exePath, bool enable)
    {
        try
        {
            ElevatedStartupTask.EnsureCreated(exePath);
            StartWithWindows.SetEnabled(enable);
        }
        catch (Exception)
        {
            // The installer cannot show our errors; the option can still be changed in Configurações.
        }
    }

    private static void StartElevatedCopy(string[] args)
    {
        // Let the elevated copy take the foreground; it is started by another process.
        AllowSetForegroundWindow(AsfwAny);

        if (ElevatedStartupTask.TryRun())
            return;

        try
        {
            var startInfo = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" };
            foreach (var arg in args)
                startInfo.ArgumentList.Add(arg);
            Process.Start(startInfo);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            // The user said "No" on the UAC prompt: nothing to open.
        }
    }

    private const int AsfwAny = -1;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int processId);
}
