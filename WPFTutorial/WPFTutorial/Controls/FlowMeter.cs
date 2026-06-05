using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WPFTutorial.Controls;

public class FlowMeter : Control
{
    static FlowMeter()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(FlowMeter),
            new FrameworkPropertyMetadata(typeof(FlowMeter)));
    }

    public double FlowRate
    {
        get => (double)GetValue(FlowRateProperty);
        set => SetValue(FlowRateProperty, value);
    }

    public static readonly DependencyProperty FlowRateProperty =
        DependencyProperty.Register(nameof(FlowRate), typeof(double), typeof(FlowMeter),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double MaxFlow
    {
        get => (double)GetValue(MaxFlowProperty);
        set => SetValue(MaxFlowProperty, value);
    }

    public static readonly DependencyProperty MaxFlowProperty =
        DependencyProperty.Register(nameof(MaxFlow), typeof(double), typeof(FlowMeter),
            new FrameworkPropertyMetadata(100.0));

    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(nameof(Unit), typeof(string), typeof(FlowMeter),
            new FrameworkPropertyMetadata("L/min"));

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var rect = new Rect(RenderSize);
        var w = rect.Width;
        var h = rect.Height;

        if (w < 20 || h < 20) return;

        // Layout
        var barHeight = h * 0.3;
        var barY = h * 0.35;
        var barMargin = w * 0.05;
        var barWidth = w - barMargin * 2;
        var cornerRadius = barHeight / 2;

        // Background bar track
        var trackRect = new Rect(barMargin, barY, barWidth, barHeight);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(40, 40, 40)), null, trackRect, cornerRadius, cornerRadius);

        // Fill bar
        var clamped = Math.Clamp(FlowRate, 0, MaxFlow);
        var fillRatio = clamped / MaxFlow;
        var fillWidth = barWidth * fillRatio;

        if (fillWidth > cornerRadius)
        {
            var fillRect = new Rect(barMargin, barY, fillWidth, barHeight);

            Color fillColor;
            if (fillRatio > 0.85) fillColor = Color.FromRgb(255, 60, 60);
            else if (fillRatio > 0.65) fillColor = Color.FromRgb(255, 200, 40);
            else fillColor = Color.FromRgb(50, 180, 220);

            var fillBrush = new LinearGradientBrush(
                Color.FromArgb(255, fillColor.R, fillColor.G, fillColor.B),
                Color.FromArgb(200, fillColor.R, fillColor.G, fillColor.B),
                0);
            dc.DrawRoundedRectangle(fillBrush, null, fillRect, cornerRadius, cornerRadius);

            // Flow animation - draw flow lines (moving dots effect)
            var dotBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
            for (int i = 0; i < 5; i++)
            {
                var dotX = barMargin + fillWidth * ((i + 1) / 6.0);
                if (dotX > barMargin && dotX < barMargin + fillWidth)
                {
                    dc.DrawEllipse(dotBrush, null, new Point(dotX, barY + barHeight / 2), 2, 2);
                }
            }
        }

        // Draw value text
        var fontSize = Math.Max(10, h * 0.2);
        var ft = new FormattedText($"{FlowRate:F1} {Unit}",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), fontSize,
            Brushes.White, 1.25);
        dc.DrawText(ft, new Point((w - ft.Width) / 2, barY + barHeight + 8));

        // Draw label
        var labelFt = new FormattedText("流量",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), fontSize * 0.7,
            new SolidColorBrush(Color.FromRgb(150, 150, 150)), 1.25);
        dc.DrawText(labelFt, new Point((w - labelFt.Width) / 2, 4));

        // Draw scale marks under the bar
        var scaleY = barY + barHeight + 4;
        for (int i = 0; i <= 10; i++)
        {
            var sx = barMargin + barWidth * i / 10;
            var sh = i % 5 == 0 ? 5 : 3;
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(80, 80, 80)), 1),
                new Point(sx, scaleY), new Point(sx, scaleY + sh));
        }
    }
}
