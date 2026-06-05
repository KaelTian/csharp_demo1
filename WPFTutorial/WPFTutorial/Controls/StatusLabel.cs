using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WPFTutorial.Controls;

public class StatusLabel : Control
{
    static StatusLabel()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(StatusLabel),
            new FrameworkPropertyMetadata(typeof(StatusLabel)));
    }

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public static readonly DependencyProperty StatusTextProperty =
        DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(StatusLabel),
            new FrameworkPropertyMetadata("正常", FrameworkPropertyMetadataOptions.AffectsRender));

    public StatusType Status
    {
        get => (StatusType)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(nameof(Status), typeof(StatusType), typeof(StatusLabel),
            new FrameworkPropertyMetadata(StatusType.Normal, FrameworkPropertyMetadataOptions.AffectsRender));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(StatusLabel),
            new FrameworkPropertyMetadata("状态"));

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var rect = new Rect(RenderSize);
        var w = rect.Width;
        var h = rect.Height;

        if (w < 10 || h < 10) return;

        var padding = 8;
        var fontHeight = Math.Max(8, h * 0.35);
        var labelHeight = Math.Max(6, h * 0.3);

        (Color bgColor, Color textColor, string statusChar) = Status switch
        {
            StatusType.Normal => (Color.FromRgb(30, 80, 40), Color.FromRgb(100, 220, 120), "●"),
            StatusType.Warning => (Color.FromRgb(80, 70, 20), Color.FromRgb(240, 210, 60), "●"),
            StatusType.Alarm => (Color.FromRgb(80, 25, 25), Color.FromRgb(255, 80, 80), "●"),
            StatusType.Offline => (Color.FromRgb(50, 50, 50), Color.FromRgb(120, 120, 120), "○"),
            _ => (Color.FromRgb(40, 40, 40), Color.FromRgb(160, 160, 160), "●"),
        };

        // Background pill
        var bgRect = new Rect(padding, h * 0.25, w - padding * 2, h * 0.55);
        dc.DrawRoundedRectangle(new SolidColorBrush(bgColor),
            new Pen(new SolidColorBrush(Color.FromRgb(60, 60, 60)), 1),
            bgRect, bgRect.Height / 2, bgRect.Height / 2);

        // Status character
        var charFt = new FormattedText(statusChar, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), fontHeight,
            new SolidColorBrush(textColor), 1.25);
        dc.DrawText(charFt, new Point(padding + 8, h * 0.25 + (bgRect.Height - charFt.Height) / 2));

        // Status text
        var ft = new FormattedText(StatusText, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), fontHeight * 0.8,
            new SolidColorBrush(textColor), 1.25);
        dc.DrawText(ft, new Point(padding + 8 + charFt.Width + 6, h * 0.25 + (bgRect.Height - ft.Height) / 2));

        // Label above
        var labelFt = new FormattedText(Label, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), labelHeight * 0.6,
            new SolidColorBrush(Color.FromRgb(140, 140, 140)), 1.25);
        dc.DrawText(labelFt, new Point(padding, 4));
    }
}

public enum StatusType
{
    Normal,
    Warning,
    Alarm,
    Offline
}
