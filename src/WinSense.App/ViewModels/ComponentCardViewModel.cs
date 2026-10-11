using CommunityToolkit.Mvvm.ComponentModel;

namespace WinSense.App.ViewModels;

/// <summary>One hardware component on the monitor page (CPU, RAM or a GPU).</summary>
public partial class ComponentCardViewModel(string kind, string name, string details) : ObservableObject
{
    public const int HistoryLength = 60;

    /// <summary>What the component is, e.g. "Processador (CPU)".</summary>
    public string Kind { get; } = kind;

    /// <summary>Its model name, e.g. "AMD Ryzen 7 7735HS with Radeon Graphics".</summary>
    public string Name { get; } = name;

    public string Details { get; } = details;

    [ObservableProperty]
    private double _usage;

    [ObservableProperty]
    private string _usageText = "–";

    /// <summary>Clock, memory in use, VRAM... depends on the component.</summary>
    [ObservableProperty]
    private string _info = "";

    /// <summary>Empty when the component has no temperature to show.</summary>
    [ObservableProperty]
    private string _temperatureText = "";

    /// <summary>Last <see cref="HistoryLength"/> usage values, oldest first. Replaced (not mutated) on each update.</summary>
    [ObservableProperty]
    private IReadOnlyList<double> _history = [];

    public void Push(double usage)
    {
        Usage = usage;
        UsageText = $"{usage:0}%";
        History = [.. History.Skip(Math.Max(0, History.Count - HistoryLength + 1)), usage];
    }
}
