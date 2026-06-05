using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WPFTutorial.Controls;

public class Tank : Control
{
    static Tank()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Tank),
            new FrameworkPropertyMetadata(typeof(Tank)));
    }

    public double Level
    {
        get => (double)GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public static readonly DependencyProperty LevelProperty =
        DependencyProperty.Register(nameof(Level), typeof(double), typeof(Tank),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(Tank),
            new FrameworkPropertyMetadata("储罐"));

    public double Capacity
    {
        get => (double)GetValue(CapacityProperty);
        set => SetValue(CapacityProperty, value);
    }

    public static readonly DependencyProperty CapacityProperty =
        DependencyProperty.Register(nameof(Capacity), typeof(double), typeof(Tank),
            new FrameworkPropertyMetadata(1000.0));

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var rect = new Rect(RenderSize);
        var w = rect.Width;
        var h = rect.Height;

        if (w < 10 || h < 10) return;

        // Tank proportions
        var bodyWidth = w * 0.6;
        var bodyLeft = (w - bodyWidth) / 2;
        var bodyTop = h * 0.1;
        var bodyHeight = h * 0.65;
        var domeWidth = bodyWidth * 1.1;
        var domeLeft = (w - domeWidth) / 2;
        var domeHeight = bodyWidth * 0.2;

        // Tank outline
        var tankPen = new Pen(new SolidColorBrush(Color.FromRgb(120, 130, 140)), 2);

        // Draw dome (roof) using arc geometry
        var domeGeo = new StreamGeometry();
        using (var domeCtx = domeGeo.Open())
        {
            var domeTop = bodyTop;
            var domeRight = domeLeft + domeWidth;
            var domeBottom = bodyTop + domeHeight * 2;
            domeCtx.BeginFigure(new Point(domeRight, domeTop + domeHeight), true, true);
            domeCtx.ArcTo(new Point(domeLeft, domeTop + domeHeight), new Size(domeWidth / 2, domeHeight), 180, false, SweepDirection.Clockwise, true, false);
            domeCtx.LineTo(new Point(domeRight, domeTop + domeHeight), true, false);
        }
        domeGeo.Freeze();
        dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(50, 55, 60)), tankPen, domeGeo);

        // Draw body walls
        var wallColor = new SolidColorBrush(Color.FromRgb(45, 50, 55));
        dc.DrawRectangle(wallColor, tankPen, new Rect(bodyLeft, bodyTop + domeHeight, bodyWidth, bodyHeight - domeHeight));

        // Draw bottom (slightly curved)
        var bottomRect = new Rect(bodyLeft - 5, bodyTop + bodyHeight - 8, bodyWidth + 10, 16);
        dc.DrawEllipse(wallColor, tankPen, new Point(w / 2, bodyTop + bodyHeight - 8), bodyWidth / 2 + 5, 8);

        // Tank legs (simple lines)
        var legColor = new Pen(new SolidColorBrush(Color.FromRgb(80, 80, 80)), 3);
        var legBottom = bodyTop + bodyHeight + 12;
        dc.DrawLine(legColor, new Point(bodyLeft + 8, bodyTop + bodyHeight - 6), new Point(bodyLeft + 8, legBottom));
        dc.DrawLine(legColor, new Point(w - bodyLeft - 8, bodyTop + bodyHeight - 6), new Point(w - bodyLeft - 8, legBottom));

        // Liquid fill
        var clampedLevel = Math.Clamp(Level, 0, 100);
        var fillHeight = clampedLevel / 100.0 * (bodyHeight - domeHeight);
        var fillTop = bodyTop + bodyHeight - fillHeight;

        Color fillColor;
        if (clampedLevel > 80) fillColor = Color.FromRgb(255, 180, 40);
        else if (clampedLevel > 40) fillColor = Color.FromRgb(60, 180, 220);
        else fillColor = Color.FromRgb(50, 140, 200);

        if (fillHeight > 0)
        {
            var fillRect = new Rect(bodyLeft + 2, fillTop, bodyWidth - 4, fillHeight);

            // Liquid gradient for depth effect
            var fillBrush = new LinearGradientBrush(
                Color.FromArgb(200, fillColor.R, fillColor.G, fillColor.B),
                Color.FromArgb(120, (byte)(fillColor.R / 2), (byte)(fillColor.G / 2), (byte)(fillColor.B / 2)),
                90);
            dc.DrawRectangle(fillBrush, null, fillRect);

            // Surface wave line
            var wavePen = new Pen(new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)), 1.5);
            for (int i = 0; i < bodyWidth; i += 3)
            {
                var waveY = fillTop + Math.Sin(i * 0.15 + Environment.TickCount64 * 0.0005) * 2;
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), null,
                    new Point(bodyLeft + 2 + i, waveY), 1, 1);
            }
        }

        // Level indicators on the side
        var markerPen = new Pen(new SolidColorBrush(Color.FromRgb(100, 100, 100)), 1);
        for (int i = 0; i <= 4; i++)
        {
            var my = bodyTop + domeHeight + (bodyHeight - domeHeight) * i / 4;
            dc.DrawLine(markerPen, new Point(bodyLeft - 5, my), new Point(bodyLeft, my));
        }

        // Value text
        var fontSize = w * 0.08;
        var ft = new FormattedText($"{clampedLevel:F1}%",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), fontSize,
            Brushes.White, 1.25);
        dc.DrawText(ft, new Point((w - ft.Width) / 2, bodyTop + bodyHeight + 16));

        // Label text
        var labelFt = new FormattedText($"{Label}\n{Capacity:F0}L",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), fontSize * 0.7,
            new SolidColorBrush(Color.FromRgb(160, 160, 160)), 1.25);
        dc.DrawText(labelFt, new Point((w - labelFt.Width) / 2, 4));
    }
}
