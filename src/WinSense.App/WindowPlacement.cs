using System.IO;
using System.Text.Json;
using System.Windows;

namespace WinSense.App;

/// <summary>Window size, position and whether it was maximized, restored on the next start.</summary>
public sealed record WindowPlacement(double Left, double Top, double Width, double Height, bool Maximized)
{
    /// <summary>Minimum visible part of the title bar for a saved position to be trusted.</summary>
    private const double MinVisible = 100;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinSense", "window.json");

    /// <summary>
    /// Keeps the saved placement only if enough of it is still on screen (a monitor may have been
    /// unplugged since); otherwise keeps just the maximized flag and lets the window center itself.
    /// </summary>
    public WindowPlacement? FitTo(Rect virtualScreen, Size minSize)
    {
        var width = Math.Max(Width, minSize.Width);
        var height = Math.Max(Height, minSize.Height);
        var titleBar = new Rect(Left, Top, width, 32);
        titleBar.Intersect(virtualScreen);

        return titleBar.IsEmpty || titleBar.Width < MinVisible || double.IsNaN(Left) || double.IsNaN(Top)
            ? null
            : this with { Width = width, Height = height };
    }

    public static WindowPlacement? Load()
    {
        try
        {
            return File.Exists(FilePath) ? JsonSerializer.Deserialize<WindowPlacement>(File.ReadAllText(FilePath)) : null;
        }
        catch (Exception)
        {
            return null; // Corrupt or unreadable: start with the defaults.
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
        }
        catch (Exception)
        {
            // Not worth bothering the user about.
        }
    }

    /// <summary>Applies a saved placement before the window is shown.</summary>
    public static void Restore(Window window)
    {
        var saved = Load();
        if (saved is null)
            return;

        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        if (saved.FitTo(screen, new Size(window.MinWidth, window.MinHeight)) is { } fit)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = fit.Left;
            window.Top = fit.Top;
            window.Width = fit.Width;
            window.Height = fit.Height;
        }

        if (saved.Maximized)
            window.WindowState = WindowState.Maximized;
    }

    /// <summary>Remembers the placement when the window closes. Minimized counts as the state before minimizing.</summary>
    public static void TrackAndSaveOnClose(Window window)
    {
        var lastState = window.WindowState;
        window.StateChanged += (_, _) =>
        {
            if (window.WindowState != WindowState.Minimized)
                lastState = window.WindowState;
        };

        window.Closing += (_, _) =>
        {
            // RestoreBounds is the normal-state rectangle, even while maximized or minimized.
            var bounds = window.WindowState == WindowState.Normal
                ? new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight)
                : window.RestoreBounds;
            if (bounds.IsEmpty)
                return;

            new WindowPlacement(bounds.Left, bounds.Top, bounds.Width, bounds.Height,
                lastState == WindowState.Maximized).Save();
        };
    }
}
