using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WPFTutorial.Controls;

public class Pump : Control
{
    static Pump()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Pump),
            new FrameworkPropertyMetadata(typeof(Pump)));
    }

    public double Speed
    {
        get => (double)GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    public static readonly DependencyProperty SpeedProperty =
        DependencyProperty.Register(nameof(Speed), typeof(double), typeof(Pump),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public bool IsRunning => Speed > 0;

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var rect = new Rect(RenderSize);
        var cx = rect.Width / 2;
        var cy = rect.Height / 2;
        var size = Math.Min(rect.Width, rect.Height);

        if (size < 10) return;
        var radius = size * 0.32;

        // Inlet pipe (left)
        var pipeThickness = size * 0.12;
        var pipeColor = new SolidColorBrush(Color.FromRgb(100, 110, 120));
        dc.DrawRectangle(pipeColor, null, new Rect(0, cy - pipeThickness / 2, cx - radius, pipeThickness));

        // Outlet pipe (top)
        dc.DrawRectangle(pipeColor, null, new Rect(cx - pipeThickness / 2, 0, pipeThickness, cy - radius));

        // Pump housing
        var housingColor = IsRunning ? Color.FromRgb(60, 120, 180) : Color.FromRgb(80, 80, 80);
        var housingBrush = new RadialGradientBrush(
            Color.FromArgb(255, (byte)(housingColor.R + 40), (byte)(housingColor.G + 40), (byte)(housingColor.B + 40)),
            housingColor);
        dc.DrawEllipse(housingBrush, new Pen(new SolidColorBrush(Color.FromRgb(150, 150, 150)), 2),
            new Point(cx, cy), radius, radius);

        // Impeller visualization (spiral or blades)
        if (IsRunning)
        {
            var bladeColor = new SolidColorBrush(Color.FromRgb(200, 220, 240));
            var bladeLen = radius * 0.5;

            for (int i = 0; i < 6; i++)
            {
                var angle = i * 60 * Math.PI / 180;
                var bx = cx + bladeLen * Math.Cos(angle);
                var by = cy + bladeLen * Math.Sin(angle);
                dc.DrawLine(new Pen(bladeColor, 2.5), new Point(cx, cy), new Point(bx, by));
            }

            // Rotation ring indicator
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(100, 100, 200, 255)), 1.5),
                new Point(cx, cy), radius * 0.6, radius * 0.6);
        }
        else
        {
            // Static blades
            for (int i = 0; i < 6; i++)
            {
                var angle = i * 60 * Math.PI / 180;
                var bx = cx + radius * 0.4 * Math.Cos(angle);
                var by = cy + radius * 0.4 * Math.Sin(angle);
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(120, 120, 120)), 2),
                    new Point(cx, cy), new Point(bx, by));
            }
        }

        // Center hub
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(50, 50, 50)), null, new Point(cx, cy), radius * 0.15, radius * 0.15);

        // Label
        var label = IsRunning ? $"{Speed:F0} rpm" : "已停止";
        var ft = new FormattedText(label, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), size * 0.13,
            IsRunning ? Brushes.LightBlue : Brushes.Gray, 1.25);
        dc.DrawText(ft, new Point(cx - ft.Width / 2, cy + radius + 8));
    }
}
