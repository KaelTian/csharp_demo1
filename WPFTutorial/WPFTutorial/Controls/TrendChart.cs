using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WPFTutorial.Controls;

public class TrendChart : Control
{
    private readonly Queue<double> _dataPoints = new();
    private const int MaxPoints = 100;

    static TrendChart()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(TrendChart),
            new FrameworkPropertyMetadata(typeof(TrendChart)));
    }

    public double NewValue
    {
        get => (double)GetValue(NewValueProperty);
        set => SetValue(NewValueProperty, value);
    }

    public static readonly DependencyProperty NewValueProperty =
        DependencyProperty.Register(nameof(NewValue), typeof(double), typeof(TrendChart),
            new FrameworkPropertyMetadata(50.0, OnNewValueChanged));

    public double MinRange
    {
        get => (double)GetValue(MinRangeProperty);
        set => SetValue(MinRangeProperty, value);
    }

    public static readonly DependencyProperty MinRangeProperty =
        DependencyProperty.Register(nameof(MinRange), typeof(double), typeof(TrendChart),
            new FrameworkPropertyMetadata(0.0));

    public double MaxRange
    {
        get => (double)GetValue(MaxRangeProperty);
        set => SetValue(MaxRangeProperty, value);
    }

    public static readonly DependencyProperty MaxRangeProperty =
        DependencyProperty.Register(nameof(MaxRange), typeof(double), typeof(TrendChart),
            new FrameworkPropertyMetadata(100.0));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(TrendChart),
            new FrameworkPropertyMetadata("趋势图"));

    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(nameof(Unit), typeof(string), typeof(TrendChart),
            new FrameworkPropertyMetadata(""));

    private static void OnNewValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TrendChart chart && e.NewValue is double val)
        {
            chart._dataPoints.Enqueue(val);
            if (chart._dataPoints.Count > MaxPoints)
                chart._dataPoints.Dequeue();
            chart.InvalidateVisual();
        }
    }

    public TrendChart()
    {
        // Initialize with some data
        var rng = new Random();
        for (int i = 0; i < 50; i++)
            _dataPoints.Enqueue(50 + Math.Sin(i * 0.2) * 15 + rng.NextDouble() * 5);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var rect = new Rect(RenderSize);
        var w = rect.Width;
        var h = rect.Height;

        if (w < 20 || h < 20) return;

        var margin = 30;
        var plotLeft = margin;
        var plotTop = 20;
        var plotWidth = w - margin - 10;
        var plotHeight = h - plotTop - margin;

        if (plotWidth <= 0 || plotHeight <= 0) return;

        var plotRect = new Rect(plotLeft, plotTop, plotWidth, plotHeight);

        // Background
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(15, 18, 22)),
            new Pen(new SolidColorBrush(Color.FromRgb(50, 55, 60)), 1),
            plotRect);

        // Grid lines
        var gridColor = new SolidColorBrush(Color.FromRgb(35, 40, 45));
        var gridCount = 5;
        for (int i = 0; i <= gridCount; i++)
        {
            // Horizontal
            var gy = plotTop + plotHeight * i / gridCount;
            dc.DrawLine(new Pen(gridColor, 0.5),
                new Point(plotLeft, gy), new Point(plotLeft + plotWidth, gy));

            // Vertical
            var gx = plotLeft + plotWidth * i / gridCount;
            dc.DrawLine(new Pen(gridColor, 0.5),
                new Point(gx, plotTop), new Point(gx, plotTop + plotHeight));
        }

        // Y-axis labels
        var axisFontSize = Math.Max(7, margin * 0.25);
        var axisBrush = new SolidColorBrush(Color.FromRgb(120, 120, 120));
        var range = MaxRange - MinRange;
        for (int i = 0; i <= gridCount; i++)
        {
            var val = MaxRange - range * i / gridCount;
            var gy = plotTop + plotHeight * i / gridCount;
            var ft = new FormattedText($"{val:F0}", System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.RightToLeft, new Typeface("Consolas"), axisFontSize, axisBrush, 1.25);
            dc.DrawText(ft, new Point(plotLeft - 4, gy - ft.Height / 2));
        }

        // Draw trend line
        if (_dataPoints.Count > 1)
        {
            var points = _dataPoints.ToArray();
            var stepX = plotWidth / (double)(MaxPoints - 1);

            var lineGeo = new StreamGeometry();
            using (var ctx = lineGeo.Open())
            {
                bool first = true;
                for (int i = 0; i < points.Length; i++)
                {
                    var x = plotLeft + (MaxPoints - points.Length + i) * stepX;
                    var ratio = (points[i] - MinRange) / range;
                    var y = plotTop + plotHeight - ratio * plotHeight;
                    y = Math.Clamp(y, plotTop, plotTop + plotHeight);

                    if (first)
                    {
                        ctx.BeginFigure(new Point(x, y), false, false);
                        first = false;
                    }
                    else
                    {
                        ctx.LineTo(new Point(x, y), true, false);
                    }
                }
            }
            lineGeo.Freeze();

            // Draw line
            var lineColor = Color.FromRgb(80, 180, 255);
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(lineColor), 1.5), lineGeo);

            // Fill under the line
            if (points.Length > 1)
            {
                var fillGeo = new StreamGeometry();
                using (var ctx = fillGeo.Open())
                {
                    int last = points.Length - 1;
                    var firstX = plotLeft + (MaxPoints - points.Length) * stepX;
                    var lastX = plotLeft + (MaxPoints - points.Length + last) * stepX;

                    var firstRatio = (points[0] - MinRange) / range;
                    var firstY = Math.Clamp(plotTop + plotHeight - firstRatio * plotHeight, plotTop, plotTop + plotHeight);

                    ctx.BeginFigure(new Point(firstX, plotTop + plotHeight), true, false);
                    ctx.LineTo(new Point(firstX, firstY), true, false);

                    for (int i = 1; i < points.Length; i++)
                    {
                        var x = plotLeft + (MaxPoints - points.Length + i) * stepX;
                        var ratio = (points[i] - MinRange) / range;
                        var y = Math.Clamp(plotTop + plotHeight - ratio * plotHeight, plotTop, plotTop + plotHeight);
                        ctx.LineTo(new Point(x, y), true, false);
                    }

                    ctx.LineTo(new Point(lastX, plotTop + plotHeight), true, false);
                }
                fillGeo.Freeze();

                dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(30, 80, 180, 255)), null, fillGeo);
            }
        }

        // Current value text
        var currentVal = _dataPoints.Count > 0 ? _dataPoints.Last() : 0;
        var valFt = new FormattedText($"{currentVal:F1} {Unit}",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), 12,
            Brushes.White, 1.25);
        dc.DrawText(valFt, new Point(plotLeft + 4, plotTop + 2));

        // Label
        var labelFt = new FormattedText(Label,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10,
            new SolidColorBrush(Color.FromRgb(140, 140, 140)), 1.25);
        dc.DrawText(labelFt, new Point(plotLeft + plotWidth - labelFt.Width, plotTop + 2));
    }
}
