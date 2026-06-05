using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WPFTutorial.Controls;

public class TemperatureGauge : Control
{
    static TemperatureGauge()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(TemperatureGauge),
            new FrameworkPropertyMetadata(typeof(TemperatureGauge)));
    }

    public double Temperature
    {
        get => (double)GetValue(TemperatureProperty);
        set => SetValue(TemperatureProperty, value);
    }

    public static readonly DependencyProperty TemperatureProperty =
        DependencyProperty.Register(nameof(Temperature), typeof(double), typeof(TemperatureGauge),
            new FrameworkPropertyMetadata(25.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double MinTemp
    {
        get => (double)GetValue(MinTempProperty);
        set => SetValue(MinTempProperty, value);
    }

    public static readonly DependencyProperty MinTempProperty =
        DependencyProperty.Register(nameof(MinTemp), typeof(double), typeof(TemperatureGauge),
            new FrameworkPropertyMetadata(-20.0));

    public double MaxTemp
    {
        get => (double)GetValue(MaxTempProperty);
        set => SetValue(MaxTempProperty, value);
    }

    public static readonly DependencyProperty MaxTempProperty =
        DependencyProperty.Register(nameof(MaxTemp), typeof(double), typeof(TemperatureGauge),
            new FrameworkPropertyMetadata(100.0));

    public double HighAlarm
    {
        get => (double)GetValue(HighAlarmProperty);
        set => SetValue(HighAlarmProperty, value);
    }

    public static readonly DependencyProperty HighAlarmProperty =
        DependencyProperty.Register(nameof(HighAlarm), typeof(double), typeof(TemperatureGauge),
            new FrameworkPropertyMetadata(80.0));

    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(nameof(Unit), typeof(string), typeof(TemperatureGauge),
            new FrameworkPropertyMetadata("°C"));

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var rect = new Rect(RenderSize);
        var w = rect.Width;
        var h = rect.Height;

        if (w < 10 || h < 10) return;

        // Thermometer body dimensions
        var bodyWidth = w * 0.25;
        var bodyLeft = (w - bodyWidth) / 2;
        var bodyTop = h * 0.1;
        var bodyHeight = h * 0.7;
        var bulbRadius = bodyWidth * 0.6;
        var bulbCenterY = bodyTop + bodyHeight + bulbRadius;
        var scaleRight = w * 0.85;

        // Draw scale background
        var scaleBg = new Rect(scaleRight - 3, bodyTop, 6, bodyHeight);
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(60, 60, 60)), null, scaleBg);

        // Draw scale ticks and labels
        var range = MaxTemp - MinTemp;
        var tickCount = 10;
        var tickFontSize = Math.Max(8, w * 0.045);
        var fgBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220));

        for (int i = 0; i <= tickCount; i++)
        {
            var val = MinTemp + range * i / tickCount;
            var ratio = 1.0 - (val - MinTemp) / range;
            var y = bodyTop + ratio * bodyHeight;
            var tickLen = i % 5 == 0 ? 8 : 4;
            dc.DrawLine(new Pen(fgBrush, 1.5), new Point(scaleRight, y), new Point(scaleRight + tickLen, y));

            if (i % 5 == 0)
            {
                var ft = new FormattedText($"{val:F0}", System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, new Typeface("Consolas"), tickFontSize, fgBrush, 1.25);
                dc.DrawText(ft, new Point(scaleRight + tickLen + 2, y - ft.Height / 2));
            }
        }

        // Draw unit label
        var unitFt = new FormattedText(Unit, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), tickFontSize + 2, fgBrush, 1.25);
        dc.DrawText(unitFt, new Point(scaleRight + 2, bodyTop - unitFt.Height - 4));

        // Draw thermometer tube (background)
        var tubeRect = new Rect(bodyLeft, bodyTop, bodyWidth, bodyHeight);
        var tubePen = new Pen(new SolidColorBrush(Color.FromRgb(80, 80, 80)), 1.5);
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(30, 30, 30)), tubePen, tubeRect);

        // Draw bulb outline
        var bulbPen = new Pen(new SolidColorBrush(Color.FromRgb(80, 80, 80)), 1.5);
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(30, 30, 30)), bulbPen,
            new Point(w / 2, bulbCenterY), bulbRadius, bulbRadius);

        // Calculate mercury fill
        var clampedValue = Math.Clamp(Temperature, MinTemp, MaxTemp);
        var fillRatio = (clampedValue - MinTemp) / range;
        var fillHeight = fillRatio * bodyHeight;
        var fillTop = bodyTop + bodyHeight - fillHeight;

        // Determine color based on temperature
        Color mercuryColor;
        if (Temperature > HighAlarm) mercuryColor = Color.FromRgb(255, 50, 50);
        else if (Temperature > HighAlarm * 0.75) mercuryColor = Color.FromRgb(255, 180, 40);
        else mercuryColor = Color.FromRgb(220, 60, 60);

        // Draw mercury column
        if (fillHeight > 0)
        {
            var mercuryBody = new Rect(bodyLeft + 2, fillTop, bodyWidth - 4, fillHeight);
            var mercuryBrush = new LinearGradientBrush(
                Color.FromArgb(200, mercuryColor.R, mercuryColor.G, mercuryColor.B),
                Color.FromArgb(240, mercuryColor.R, mercuryColor.G, mercuryColor.B),
                0);
            dc.DrawRectangle(mercuryBrush, null, mercuryBody);

            // Mercury meniscus (top rounding)
            if (fillHeight > bodyWidth)
            {
                dc.DrawEllipse(mercuryBrush, null,
                    new Point(w / 2, fillTop), (bodyWidth - 4) / 2, 3);
            }
        }

        // Draw bulb fill
        if (fillRatio > 0)
        {
            var bulbFill = new RadialGradientBrush(
                Color.FromArgb(220, mercuryColor.R, mercuryColor.G, mercuryColor.B),
                Color.FromArgb(180, mercuryColor.R, mercuryColor.G, mercuryColor.B));
            dc.DrawEllipse(bulbFill, null,
                new Point(w / 2, bulbCenterY), bulbRadius * 0.85, bulbRadius * 0.85);
        }

        // Draw value text
        var valueFt = new FormattedText($"{Temperature:F1}{Unit}",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), tickFontSize + 4,
            Temperature > HighAlarm ? Brushes.Red : Brushes.White, 1.25);
        dc.DrawText(valueFt, new Point((w - valueFt.Width) / 2, bulbCenterY + bulbRadius + 8));
    }
}
