using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WPFTutorial.Controls;

public class Motor : Control
{
    static Motor()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Motor),
            new FrameworkPropertyMetadata(typeof(Motor)));
    }

    public double Speed
    {
        get => (double)GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    public static readonly DependencyProperty SpeedProperty =
        DependencyProperty.Register(nameof(Speed), typeof(double), typeof(Motor),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public bool IsRunning => Speed > 0;

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(Motor),
            new FrameworkPropertyMetadata("电机"));

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var rect = new Rect(RenderSize);
        var cx = rect.Width / 2;
        var cy = rect.Height / 2;
        var size = Math.Min(rect.Width, rect.Height);

        if (size < 10) return;
        var radius = size * 0.3;

        // Motor body (rounded rectangle)
        var bodyWidth = radius * 1.8;
        var bodyHeight = radius * 1.4;
        var bodyLeft = cx - bodyWidth / 2;
        var bodyTop = cy - bodyHeight / 2;
        var bodyRect = new Rect(bodyLeft, bodyTop, bodyWidth, bodyHeight);

        var bodyColor = IsRunning ? Color.FromRgb(60, 110, 160) : Color.FromRgb(70, 70, 70);
        var bodyBrush = new LinearGradientBrush(
            Color.FromArgb(255, (byte)(bodyColor.R + 40), (byte)(bodyColor.G + 40), (byte)(bodyColor.B + 40)),
            bodyColor, 90);

        dc.DrawRoundedRectangle(bodyBrush,
            new Pen(new SolidColorBrush(Color.FromRgb(130, 130, 130)), 1.5),
            bodyRect, 5, 5);

        // Cooling fins (horizontal lines)
        var finColor = new Pen(new SolidColorBrush(Color.FromRgb(50, 50, 50)), 1.5);
        for (int i = 0; i < 4; i++)
        {
            var fy = bodyTop + bodyHeight * (i + 1) / 5;
            dc.DrawLine(finColor, new Point(bodyLeft + 4, fy), new Point(bodyLeft + bodyWidth - 4, fy));
        }

        // Shaft (extending to the right)
        if (IsRunning)
        {
            var shaftBrush = new SolidColorBrush(Color.FromRgb(180, 180, 180));
            dc.DrawRectangle(shaftBrush, null, new Rect(cx + bodyWidth / 2 - 2, cy - 3, size * 0.15, 6));

            // Rotation indicator
            var rotRadius = 5;
            var rotCy = cy;
            var rotCx = cx + bodyWidth / 2 + size * 0.08;
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(200, 100, 50)), null,
                new Point(rotCx, rotCy), rotRadius, rotRadius);
            dc.DrawLine(new Pen(Brushes.White, 1.5),
                new Point(rotCx, rotCy),
                new Point(rotCx + rotRadius * 0.8 * Math.Cos(Environment.TickCount64 * 0.003),
                    rotCy + rotRadius * 0.8 * Math.Sin(Environment.TickCount64 * 0.003)));
        }
        else
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(100, 100, 100)), null,
                new Rect(cx + bodyWidth / 2 - 2, cy - 3, size * 0.12, 6));
        }

        // Mounting base
        var baseRect = new Rect(cx - bodyWidth * 0.6, bodyTop + bodyHeight - 3, bodyWidth * 1.2, 6);
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(60, 60, 60)), null, baseRect);

        // Nameplate
        var nameColor = IsRunning ? Brushes.LightBlue : Brushes.Gray;
        var ft = new FormattedText(Label, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), size * 0.12,
            nameColor, 1.25);
        dc.DrawText(ft, new Point(cx - ft.Width / 2, bodyTop + 4));

        // Speed value
        var speedFt = new FormattedText(IsRunning ? $"{Speed:F0} rpm" : "停止",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), size * 0.14,
            nameColor, 1.25);
        dc.DrawText(speedFt, new Point(cx - speedFt.Width / 2, bodyTop + bodyHeight / 2 - speedFt.Height / 2));
    }
}
