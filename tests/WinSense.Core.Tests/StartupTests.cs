using System.IO;
using System.Windows;
using System.Xml.Linq;
using WinSense.App;
using WinSense.Core.Elevation;

namespace WinSense.Core.Tests;

public class WindowPlacementTests
{
    private static readonly Rect Screen = new(0, 0, 1920, 1080);
    private static readonly Size MinSize = new(680, 420);

    [Fact]
    public void OnScreen_IsKept()
    {
        var saved = new WindowPlacement(100, 80, 1040, 680, Maximized: true);

        Assert.Equal(saved, saved.FitTo(Screen, MinSize));
    }

    [Fact]
    public void OnUnpluggedMonitor_IsDropped()
    {
        var saved = new WindowPlacement(2500, 100, 1040, 680, Maximized: false);

        Assert.Null(saved.FitTo(Screen, MinSize));
    }

    [Fact]
    public void BarelyVisible_IsDropped()
    {
        var saved = new WindowPlacement(1880, 100, 1040, 680, Maximized: false);

        Assert.Null(saved.FitTo(Screen, MinSize));
    }

    [Fact]
    public void TooSmall_GrowsToMinimum()
    {
        var fit = new WindowPlacement(10, 10, 100, 50, Maximized: false).FitTo(Screen, MinSize);

        Assert.Equal(680, fit!.Width);
        Assert.Equal(420, fit.Height);
    }
}

public class ElevatedStartupTaskTests
{
    [Fact]
    public void ProgramFiles_IsProtected()
    {
        var exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WinSense", "WinSense.exe");

        Assert.True(ElevatedStartupTask.IsProtectedLocation(exe));
    }

    [Theory]
    [InlineData(@"C:\Users\fulano\Downloads\WinSense\WinSense.exe")]
    [InlineData(@"C:\Program Files Evil\WinSense.exe")] // prefix trick: not inside Program Files
    public void UserFolders_AreNotProtected(string exe) =>
        Assert.False(ElevatedStartupTask.IsProtectedLocation(exe));

    private static readonly XNamespace Ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    [Fact]
    public void TaskXml_IsValidAndRunsElevatedWithoutBatteryLimits()
    {
        var xml = XDocument.Parse(ElevatedStartupTask.BuildTaskXml("S-1-5-21-1", @"C:\Program Files\A&B\WinSense.exe", "Descrição"));

        Assert.Equal("HighestAvailable", xml.Descendants(Ns + "RunLevel").Single().Value);
        Assert.Equal("S-1-5-21-1", xml.Descendants(Ns + "UserId").Single().Value);
        Assert.Equal("false", xml.Descendants(Ns + "DisallowStartIfOnBatteries").Single().Value);
        Assert.Equal("PT0S", xml.Descendants(Ns + "ExecutionTimeLimit").Single().Value);
        Assert.Equal("\"C:\\Program Files\\A&B\\WinSense.exe\"", xml.Descendants(Ns + "Command").Single().Value);
        Assert.Empty(xml.Descendants(Ns + "Triggers"));
        Assert.Empty(xml.Descendants(Ns + "Arguments"));
    }

    [Fact]
    public void LogonTaskXml_StartsHiddenWhenThisUserSignsIn()
    {
        var xml = XDocument.Parse(ElevatedStartupTask.BuildTaskXml("S-1-5-21-1", @"C:\Program Files\WinSense\WinSense.exe",
            "Descrição", StartWithWindows.BackgroundArgument, atLogon: true));

        var trigger = xml.Descendants(Ns + "LogonTrigger").Single();
        Assert.Equal("S-1-5-21-1", trigger.Element(Ns + "UserId")!.Value);
        Assert.Equal("--background", xml.Descendants(Ns + "Arguments").Single().Value);
        Assert.Equal("HighestAvailable", xml.Descendants(Ns + "RunLevel").Single().Value);
    }

    [Fact]
    public void StartWithWindows_PortableCopy_IsNotAvailable() =>
        Assert.False(new StartWithWindows(@"C:\Users\fulano\Downloads\WinSense\WinSense.exe").IsAvailable);
}

public class SettingsViewModelTests
{
    private sealed class FakeStartWithWindows(bool available, bool enabled) : IStartWithWindows
    {
        public bool Enabled { get; private set; } = enabled;
        public bool Fail { get; init; }
        public bool IsAvailable => available;
        public bool IsEnabled() => Enabled;

        public void SetEnabled(bool value)
        {
            if (Fail)
                throw new UnauthorizedAccessException("sem permissão");
            Enabled = value;
        }
    }

    [Fact]
    public async Task Load_ShowsCurrentState()
    {
        var vm = new App.ViewModels.SettingsViewModel(new FakeStartWithWindows(available: true, enabled: true));

        await vm.LoadAsync();

        Assert.True(vm.StartWithWindows);
        Assert.True(vm.CanChangeStartWithWindows);
    }

    [Fact]
    public async Task Toggle_Changes()
    {
        var fake = new FakeStartWithWindows(available: true, enabled: false);
        var vm = new App.ViewModels.SettingsViewModel(fake);
        await vm.LoadAsync();

        await vm.SetStartWithWindowsAsync(true);

        Assert.True(fake.Enabled);
        Assert.True(vm.StartWithWindows);
    }

    [Fact]
    public async Task Failure_KeepsStateAndReports()
    {
        var vm = new App.ViewModels.SettingsViewModel(new FakeStartWithWindows(available: true, enabled: false) { Fail = true });
        await vm.LoadAsync();

        await vm.SetStartWithWindowsAsync(true);

        Assert.False(vm.StartWithWindows);
        Assert.Contains("sem permissão", vm.StatusMessage);
    }

    [Fact]
    public async Task Portable_IsDisabledWithExplanation()
    {
        var vm = new App.ViewModels.SettingsViewModel(new FakeStartWithWindows(available: false, enabled: false));

        await vm.LoadAsync();

        Assert.False(vm.CanChangeStartWithWindows);
        Assert.Contains("versão instalada", vm.StatusMessage);
    }
}
