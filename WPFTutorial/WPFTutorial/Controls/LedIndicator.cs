using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WPFTutorial.Controls;

public class LedIndicator : Control
{
    static LedIndicator()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(LedIndicator),
            new FrameworkPropertyMetadata(typeof(LedIndicator)));
    }

    public bool IsOn
    {
        get => (bool)GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }

    public static readonly DependencyProperty IsOnProperty =
        DependencyProperty.Register(nameof(IsOn), typeof(bool), typeof(LedIndicator),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public Color OnColor
    {
        get => (Color)GetValue(OnColorProperty);
        set => SetValue(OnColorProperty, value);
    }

    public static readonly DependencyProperty OnColorProperty =
        DependencyProperty.Register(nameof(OnColor), typeof(Color), typeof(LedIndicator),
            new FrameworkPropertyMetadata(Colors.LimeGreen, FrameworkPropertyMetadataOptions.AffectsRender));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(LedIndicator),
            new FrameworkPropertyMetadata("LED"));

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var rect = new Rect(RenderSize);
        var cx = rect.Width / 2;
        var w = rect.Width;
        var h = rect.Height;

        if (w < 10 || h < 10) return;

        var ledSize = Math.Min(w, h * 0.6) * 0.45;
        if (ledSize < 2) ledSize = 2;
        var ledCy = h * 0.35;

        // Outer ring
        var outerColor = Color.FromRgb(60, 60, 60);
        dc.DrawEllipse(new SolidColorBrush(outerColor),
            new Pen(new SolidColorBrush(Color.FromRgb(40, 40, 40)), 1.5),
            new Point(cx, ledCy), ledSize, ledSize);

        if (IsOn)
        {
            // LED on - bright with glow
            var glowBrush = new RadialGradientBrush(
                Color.FromArgb(80, OnColor.R, OnColor.G, OnColor.B),
                Colors.Transparent);
            dc.DrawEllipse(glowBrush, null,
                new Point(cx, ledCy), ledSize * 1.8, ledSize * 1.8);

            var ledBrush = new RadialGradientBrush(
                Colors.White,
                OnColor);
            dc.DrawEllipse(ledBrush, null,
                new Point(cx, ledCy), ledSize * 0.8, ledSize * 0.8);

            // Highlight
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)), null,
                new Point(cx - ledSize * 0.2, ledCy - ledSize * 0.2), ledSize * 0.25, ledSize * 0.25);
        }
        else
        {
            // LED off - dark
            var offBrush = new RadialGradientBrush(
                Color.FromRgb(60, 60, 60),
                Color.FromRgb(30, 30, 30));
            dc.DrawEllipse(offBrush, null,
                new Point(cx, ledCy), ledSize * 0.8, ledSize * 0.8);
        }

        // Label
        var labelFt = new FormattedText(Label, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), ledSize * 0.45,
            new SolidColorBrush(Color.FromRgb(180, 180, 180)), 1.25);
        dc.DrawText(labelFt, new Point((w - labelFt.Width) / 2, ledCy + ledSize + 6));

        // State text
        var stateTxt = IsOn ? "ON" : "OFF";
        var stateColor = IsOn ? OnColor : Color.FromRgb(100, 100, 100);
        var stateFt = new FormattedText(stateTxt, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Consolas"), ledSize * 0.4,
            new SolidColorBrush(stateColor), 1.25);
        dc.DrawText(stateFt, new Point((w - stateFt.Width) / 2, ledCy + ledSize + ledSize * 0.45 + 6));
    }
}
