using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WPFTutorial.Controls;

public class AlarmLight : Control
{
    private readonly System.Timers.Timer _flashTimer;
    private bool _flashState;

    static AlarmLight()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(AlarmLight),
            new FrameworkPropertyMetadata(typeof(AlarmLight)));
    }

    public AlarmLight()
    {
        _flashTimer = new System.Timers.Timer(500);
        _flashTimer.Elapsed += (_, _) =>
        {
            _flashState = !_flashState;
            Dispatcher?.Invoke(() => InvalidateVisual());
        };
        _flashTimer.Start();
    }

    public bool IsAlarm
    {
        get => (bool)GetValue(IsAlarmProperty);
        set => SetValue(IsAlarmProperty, value);
    }

    public static readonly DependencyProperty IsAlarmProperty =
        DependencyProperty.Register(nameof(IsAlarm), typeof(bool), typeof(AlarmLight),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(AlarmLight),
            new FrameworkPropertyMetadata("报警"));

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var rect = new Rect(RenderSize);
        var cx = rect.Width / 2;
        var w = rect.Width;
        var h = rect.Height;

        if (w < 10 || h < 10) return;

        var size = Math.Min(w, h);
        var lightSize = size * 0.35;
        var lightY = h * 0.3;

        // Light housing (stacked circles for alarm tower)
        var housingColor = Color.FromRgb(50, 50, 50);

        // Base
        dc.DrawRectangle(new SolidColorBrush(housingColor), null,
            new Rect(cx - lightSize * 0.4, lightY + lightSize * 0.5, lightSize * 0.8, lightSize * 0.4));

        if (IsAlarm && _flashState)
        {
            // Flashing light
            // Glow effect
            var glowColor = Color.FromRgb(255, 50, 50);
            var glowBrush = new RadialGradientBrush(
                Color.FromArgb(60, 255, 100, 100),
                Colors.Transparent);
            dc.DrawEllipse(glowBrush, null,
                new Point(cx, lightY), lightSize * 2, lightSize * 2);

            // Light bulb
            var bulbBrush = new RadialGradientBrush(
                Color.FromRgb(255, 255, 200),
                Color.FromRgb(255, 50, 50));
            dc.DrawEllipse(bulbBrush,
                new Pen(new SolidColorBrush(Color.FromRgb(200, 50, 50)), 1.5),
                new Point(cx, lightY), lightSize * 0.7, lightSize * 0.7);

            // Reflector
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), null,
                new Point(cx - lightSize * 0.15, lightY - lightSize * 0.15), lightSize * 0.2, lightSize * 0.2);

            // Exclamation mark
            var exFt = new FormattedText("!", System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, new Typeface("Segoe UI Black"), lightSize * 0.8,
                Brushes.White, 1.25);
            dc.DrawText(exFt, new Point(cx - exFt.Width / 2, lightY - exFt.Height / 2));
        }
        else if (IsAlarm)
        {
            // Dim (between flashes)
            var dimBrush = new RadialGradientBrush(
                Color.FromRgb(100, 40, 40),
                Color.FromRgb(60, 20, 20));
            dc.DrawEllipse(dimBrush,
                new Pen(new SolidColorBrush(Color.FromRgb(80, 30, 30)), 1.5),
                new Point(cx, lightY), lightSize * 0.7, lightSize * 0.7);
        }
        else
        {
            // Off
            var offBrush = new RadialGradientBrush(
                Color.FromRgb(50, 50, 50),
                Color.FromRgb(30, 30, 30));
            dc.DrawEllipse(offBrush,
                new Pen(new SolidColorBrush(Color.FromRgb(60, 60, 60)), 1.5),
                new Point(cx, lightY), lightSize * 0.7, lightSize * 0.7);
        }

        // Label
        var labelFt = new FormattedText(Label, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), size * 0.1,
            IsAlarm ? Brushes.Red : new SolidColorBrush(Color.FromRgb(140, 140, 140)), 1.25);
        dc.DrawText(labelFt, new Point((w - labelFt.Width) / 2, lightY + lightSize + 10));
    }
}
