using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WPFTutorial.Controls;

public class PressureGauge : Control
{
    static PressureGauge()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(PressureGauge),
            new FrameworkPropertyMetadata(typeof(PressureGauge)));
    }

    public double Pressure
    {
        get => (double)GetValue(PressureProperty);
        set => SetValue(PressureProperty, value);
    }

    public static readonly DependencyProperty PressureProperty =
        DependencyProperty.Register(nameof(Pressure), typeof(double), typeof(PressureGauge),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double MaxPressure
    {
        get => (double)GetValue(MaxPressureProperty);
        set => SetValue(MaxPressureProperty, value);
    }

    public static readonly DependencyProperty MaxPressureProperty =
        DependencyProperty.Register(nameof(MaxPressure), typeof(double), typeof(PressureGauge),
            new FrameworkPropertyMetadata(1.6));

    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(nameof(Unit), typeof(string), typeof(PressureGauge),
            new FrameworkPropertyMetadata("MPa"));

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var rect = new Rect(RenderSize);
        var cx = rect.Width / 2;
        var cy = rect.Height * 0.55;
        var radius = Math.Min(rect.Width, rect.Height * 1.1) / 2 - 10;

        if (radius < 20) return;

        var startAngle = -120.0;
        var endAngle = 120.0;
        var range = endAngle - startAngle;
        var fgBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220));
        var dimBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100));

        // Draw outer ring
        var outerPen = new Pen(new SolidColorBrush(Color.FromRgb(70, 70, 70)), 3);
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(30, 30, 30)), outerPen, new Point(cx, cy), radius, radius);

        // Draw inner face
        var innerRadius = radius - 15;
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(20, 20, 20)), null, new Point(cx, cy), innerRadius, innerRadius);

        // Draw colored arcs (zones)
        DrawArcZone(dc, cx, cy, innerRadius - 2, startAngle, startAngle + range * 0.6, Color.FromRgb(50, 180, 70), 8);
        DrawArcZone(dc, cx, cy, innerRadius - 2, startAngle + range * 0.6, startAngle + range * 0.85, Color.FromRgb(220, 200, 40), 8);
        DrawArcZone(dc, cx, cy, innerRadius - 2, startAngle + range * 0.85, endAngle, Color.FromRgb(220, 40, 40), 8);

        // Draw tick marks and labels
        var tickCount = 16;
        var labelCount = 8;
        var tickFontSize = Math.Max(8, radius * 0.12);

        for (int i = 0; i <= tickCount; i++)
        {
            var angle = startAngle + range * i / tickCount;
            var rad = angle * Math.PI / 180;
            var isMajor = i % 2 == 0;

            var innerR = innerRadius - (isMajor ? 12 : 6);
            var outerR = innerRadius - 2;

            var x1 = cx + innerR * Math.Sin(rad);
            var y1 = cy - innerR * Math.Cos(rad);
            var x2 = cx + outerR * Math.Sin(rad);
            var y2 = cy - outerR * Math.Cos(rad);

            dc.DrawLine(new Pen(isMajor ? fgBrush : dimBrush, isMajor ? 2 : 1),
                new Point(x1, y1), new Point(x2, y2));

            if (isMajor && i <= labelCount * 2)
            {
                var labelVal = MaxPressure * i / tickCount;
                var labelR = innerRadius - 16;
                var lx = cx + labelR * Math.Sin(rad);
                var ly = cy - labelR * Math.Cos(rad);
                var ft = new FormattedText($"{labelVal:F1}", System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, new Typeface("Consolas"), tickFontSize, fgBrush, 1.25);
                dc.DrawText(ft, new Point(lx - ft.Width / 2, ly - ft.Height / 2));
            }
        }

        // Draw unit label
        var unitText = new FormattedText(Unit, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), tickFontSize, dimBrush, 1.25);
        dc.DrawText(unitText, new Point(cx - unitText.Width / 2, cy - unitText.Height / 2 + 8));

        // Draw needle
        var clamped = Math.Clamp(Pressure, 0, MaxPressure);
        var needleAngle = startAngle + range * clamped / MaxPressure;
        var needleRad = needleAngle * Math.PI / 180;
        var needleLen = innerRadius - 16;

        var nx = cx + needleLen * Math.Sin(needleRad);
        var ny = cy - needleLen * Math.Cos(needleRad);

        var needleColor = Pressure > MaxPressure * 0.85 ? Brushes.Red : Brushes.OrangeRed;
        dc.DrawLine(new Pen(needleColor, 2.5), new Point(cx, cy), new Point(nx, ny));

        // Draw center cap
        dc.DrawEllipse(Brushes.OrangeRed, new Pen(fgBrush, 1.5), new Point(cx, cy), 5, 5);

        // Draw value text at bottom
        var valueFt = new FormattedText($"{Pressure:F2}",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), tickFontSize + 4,
            Pressure > MaxPressure * 0.85 ? Brushes.Red : Brushes.White, 1.25);
        dc.DrawText(valueFt, new Point(cx - valueFt.Width / 2, cy + radius * 0.55));
    }

    private static void DrawArcZone(DrawingContext dc, double cx, double cy, double radius,
        double startAngle, double endAngle, Color color, double thickness)
    {
        // Draw zone arcs using simple pie-slice approach
        var steps = 30;
        var angleRange = endAngle - startAngle;
        var pt = new StreamGeometry();
        using var ctx = pt.Open();
        bool first = true;

        for (int i = 0; i <= steps; i++)
        {
            var angle = (startAngle + angleRange * i / steps) * Math.PI / 180;
            var x = cx + radius * Math.Sin(angle);
            var y = cy - radius * Math.Cos(angle);

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

        pt.Freeze();
        dc.DrawGeometry(null, new Pen(new SolidColorBrush(color), thickness), pt);
    }
}
