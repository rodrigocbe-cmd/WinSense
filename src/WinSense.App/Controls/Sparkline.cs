using System.Windows;
using System.Windows.Media;

namespace WinSense.App.Controls;

/// <summary>Small usage chart (0–100%) that fills from the right as values arrive.</summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double>), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(Sparkline),
        new FrameworkPropertyMetadata(Brushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CapacityProperty = DependencyProperty.Register(
        nameof(Capacity), typeof(int), typeof(Sparkline),
        new FrameworkPropertyMetadata(60, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    /// <summary>How many points fit across the full width.</summary>
    public int Capacity
    {
        get => (int)GetValue(CapacityProperty);
        set => SetValue(CapacityProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;

        // Baseline, so an idle component still shows an (empty) chart.
        var gridPen = new Pen(Stroke.CloneCurrentValue(), 1) { Brush = { Opacity = 0.25 } };
        dc.DrawLine(gridPen, new Point(0, h - 0.5), new Point(w, h - 0.5));

        var values = Values;
        if (values is null || values.Count < 2 || w <= 0 || h <= 0)
            return;

        var step = w / Math.Max(1, Capacity - 1);
        var startX = w - step * (values.Count - 1);
        Point At(int i) => new(startX + i * step, h - Math.Clamp(values[i], 0, 100) / 100 * (h - 2) - 1);

        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            ctx.BeginFigure(At(0), false, false);
            for (var i = 1; i < values.Count; i++)
                ctx.LineTo(At(i), true, true);
        }
        line.Freeze();

        var area = new StreamGeometry();
        using (var ctx = area.Open())
        {
            ctx.BeginFigure(new Point(startX, h), true, true);
            for (var i = 0; i < values.Count; i++)
                ctx.LineTo(At(i), true, true);
            ctx.LineTo(new Point(w, h), true, true);
        }
        area.Freeze();

        var fill = Stroke.CloneCurrentValue();
        fill.Opacity = 0.18;
        dc.DrawGeometry(fill, null, area);
        dc.DrawGeometry(null, new Pen(Stroke, 1.5) { LineJoin = PenLineJoin.Round }, line);
    }
}
