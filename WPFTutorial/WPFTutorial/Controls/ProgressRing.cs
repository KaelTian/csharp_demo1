using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WPFTutorial.Controls;

public class ProgressRing : Control
{
    static ProgressRing()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ProgressRing),
            new FrameworkPropertyMetadata(typeof(ProgressRing)));
    }

    public double Percentage
    {
        get => (double)GetValue(PercentageProperty);
        set => SetValue(PercentageProperty, value);
    }

    public static readonly DependencyProperty PercentageProperty =
        DependencyProperty.Register(nameof(Percentage), typeof(double), typeof(ProgressRing),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(ProgressRing),
            new FrameworkPropertyMetadata("Progress"));

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var rect = new Rect(RenderSize);
        var cx = rect.Width / 2;
        var cy = rect.Height / 2;
        var radius = Math.Min(cx, cy) - 8;

        if (radius < 10) return;

        var thickness = radius * 0.22;
        var innerRadius = radius - thickness / 2;

        // Draw background ring
        var bgPen = new Pen(new SolidColorBrush(Color.FromRgb(50, 50, 50)), thickness);
        dc.DrawEllipse(null, bgPen, new Point(cx, cy), innerRadius, innerRadius);

        // Draw progress arc
        var clamped = Math.Clamp(Percentage, 0, 100);
        var sweepAngle = 360.0 * clamped / 100.0;

        Color progressColor;
        if (clamped >= 90) progressColor = Color.FromRgb(255, 60, 60);
        else if (clamped >= 70) progressColor = Color.FromRgb(255, 200, 40);
        else progressColor = Color.FromRgb(50, 200, 100);

        if (sweepAngle > 0)
        {
            DrawArc(dc, cx, cy, innerRadius, -90, -90 + sweepAngle, progressColor, thickness);
        }

        // Draw percentage text
        var fontSize = radius * 0.5;
        var pctFt = new FormattedText($"{clamped:F0}%",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), fontSize,
            new SolidColorBrush(progressColor), 1.25);
        dc.DrawText(pctFt, new Point(cx - pctFt.Width / 2, cy - pctFt.Height));

        // Draw label
        var labelFt = new FormattedText(Label,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), fontSize * 0.45,
            new SolidColorBrush(Color.FromRgb(140, 140, 140)), 1.25);
        dc.DrawText(labelFt, new Point(cx - labelFt.Width / 2, cy + fontSize * 0.2));
    }

    private static void DrawArc(DrawingContext dc, double cx, double cy, double radius,
        double startDeg, double endDeg, Color color, double thickness)
    {
        var steps = 60;
        var angleRange = endDeg - startDeg;
        if (angleRange <= 0) return;

        var geo = new StreamGeometry();
        using var ctx = geo.Open();
        bool first = true;

        for (int i = 0; i <= steps; i++)
        {
            var angle = (startDeg + angleRange * i / steps) * Math.PI / 180;
            var x = cx + radius * Math.Cos(angle);
            var y = cy + radius * Math.Sin(angle);

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

        geo.Freeze();
        dc.DrawGeometry(null, new Pen(new SolidColorBrush(color), thickness), geo);
    }
}
