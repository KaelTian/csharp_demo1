using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WPFTutorial.Controls;

public class Valve : Control
{
    static Valve()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Valve),
            new FrameworkPropertyMetadata(typeof(Valve)));
    }

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(Valve),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var rect = new Rect(RenderSize);
        var cx = rect.Width / 2;
        var cy = rect.Height / 2;
        var size = Math.Min(rect.Width, rect.Height);

        if (size < 10) return;
        var bodySize = size * 0.5;

        // Draw pipes (horizontal)
        var pipeThickness = size * 0.15;
        var pipeColor = new SolidColorBrush(Color.FromRgb(100, 110, 120));

        // Left pipe
        dc.DrawRectangle(pipeColor, null, new Rect(0, cy - pipeThickness / 2, cx - bodySize, pipeThickness));
        // Right pipe
        dc.DrawRectangle(pipeColor, null, new Rect(cx + bodySize, cy - pipeThickness / 2, cx - bodySize, pipeThickness));

        // Valve body (diamond/butterfly)
        var bodyPoints = new PointCollection
        {
            new Point(cx - bodySize, cy),
            new Point(cx, cy - bodySize),
            new Point(cx + bodySize, cy),
            new Point(cx, cy + bodySize),
        };

        var bodyColor = IsOpen ? Color.FromRgb(70, 140, 200) : Color.FromRgb(180, 80, 60);
        var bodyGeo = new StreamGeometry();
        using (var ctx = bodyGeo.Open())
        {
            ctx.BeginFigure(bodyPoints[0], true, true);
            ctx.LineTo(bodyPoints[1], true, false);
            ctx.LineTo(bodyPoints[2], true, false);
            ctx.LineTo(bodyPoints[3], true, false);
        }
        bodyGeo.Freeze();
        dc.DrawGeometry(new SolidColorBrush(bodyColor), new Pen(new SolidColorBrush(Color.FromRgb(200, 200, 200)), 1.5), bodyGeo);

        // Handle (vertical/horizontal line through center)
        var handleColor = Brushes.OrangeRed;
        var handleLen = size * 0.3;
        if (IsOpen)
        {
            // Open = handle vertical (parallel to flow)
            dc.DrawLine(new Pen(handleColor, 3), new Point(cx, cy - handleLen), new Point(cx, cy + handleLen));
        }
        else
        {
            // Closed = handle horizontal (perpendicular to flow)
            dc.DrawLine(new Pen(handleColor, 3), new Point(cx - handleLen, cy), new Point(cx + handleLen, cy));
        }

        // Center hub
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(60, 60, 60)), new Pen(handleColor, 1), new Point(cx, cy), 4, 4);

        // Label
        var label = IsOpen ? "已开启" : "已关闭";
        var ft = new FormattedText(label, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), size * 0.15,
            IsOpen ? Brushes.LimeGreen : Brushes.Red, 1.25);
        dc.DrawText(ft, new Point(cx - ft.Width / 2, cy + bodySize + 6));
    }
}
