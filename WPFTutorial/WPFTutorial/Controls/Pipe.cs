using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WPFTutorial.Controls;

public class Pipe : Control
{
    static Pipe()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Pipe),
            new FrameworkPropertyMetadata(typeof(Pipe)));
    }

    public bool IsHorizontal
    {
        get => (bool)GetValue(IsHorizontalProperty);
        set => SetValue(IsHorizontalProperty, value);
    }

    public static readonly DependencyProperty IsHorizontalProperty =
        DependencyProperty.Register(nameof(IsHorizontal), typeof(bool), typeof(Pipe),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public bool HasFlow
    {
        get => (bool)GetValue(HasFlowProperty);
        set => SetValue(HasFlowProperty, value);
    }

    public static readonly DependencyProperty HasFlowProperty =
        DependencyProperty.Register(nameof(HasFlow), typeof(bool), typeof(Pipe),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public double PipeFlowDirection
    {
        get => (double)GetValue(PipeFlowDirectionProperty);
        set => SetValue(PipeFlowDirectionProperty, value);
    }

    public static readonly DependencyProperty PipeFlowDirectionProperty =
        DependencyProperty.Register(nameof(PipeFlowDirection), typeof(double), typeof(Pipe),
            new FrameworkPropertyMetadata(1.0));

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var rect = new Rect(RenderSize);
        var w = rect.Width;
        var h = rect.Height;

        var pipeThickness = IsHorizontal ? h * 0.35 : w * 0.35;
        var flangeWidth = pipeThickness * 0.3;

        // Pipe body color
        var pipeColor = Color.FromRgb(100, 110, 120);
        var highlightColor = Color.FromRgb(140, 150, 160);
        var shadowColor = Color.FromRgb(70, 80, 90);
        var innerColor = Color.FromRgb(30, 35, 40);

        if (IsHorizontal)
        {
            var pipeY = (h - pipeThickness) / 2;

            // Flanges
            var flangeRect = new Rect(0, pipeY - 3, flangeWidth, pipeThickness + 6);
            dc.DrawRectangle(new SolidColorBrush(pipeColor), null, flangeRect);
            flangeRect = new Rect(w - flangeWidth, pipeY - 3, flangeWidth, pipeThickness + 6);
            dc.DrawRectangle(new SolidColorBrush(pipeColor), null, flangeRect);

            // Pipe body
            var bodyRect = new Rect(flangeWidth, pipeY, w - flangeWidth * 2, pipeThickness);
            var bodyBrush = new LinearGradientBrush(highlightColor, shadowColor, 90);
            dc.DrawRectangle(bodyBrush, null, bodyRect);

            // Inner flow area
            var innerThickness = pipeThickness * 0.5;
            var innerY = pipeY + (pipeThickness - innerThickness) / 2;
            var innerRect = new Rect(flangeWidth + 2, innerY, w - flangeWidth * 2 - 4, innerThickness);
            dc.DrawRectangle(new SolidColorBrush(innerColor), null, innerRect);

            // Flow indicator (moving dots)
            if (HasFlow)
            {
                var flowColor = Color.FromArgb(80, 100, 200, 255);
                for (int i = 0; i < 6; i++)
                {
                    var dx = flangeWidth + 10 + (w - flangeWidth * 2 - 20) * i / 5.0;
                    dc.DrawEllipse(new SolidColorBrush(flowColor), null,
                        new Point(dx, h / 2), 2, 2);
                }
            }
        }
        else
        {
            var pipeX = (w - pipeThickness) / 2;

            // Flanges
            var flangeRect = new Rect(pipeX - 3, 0, pipeThickness + 6, flangeWidth);
            dc.DrawRectangle(new SolidColorBrush(pipeColor), null, flangeRect);
            flangeRect = new Rect(pipeX - 3, h - flangeWidth, pipeThickness + 6, flangeWidth);
            dc.DrawRectangle(new SolidColorBrush(pipeColor), null, flangeRect);

            // Pipe body
            var bodyRect = new Rect(pipeX, flangeWidth, pipeThickness, h - flangeWidth * 2);
            var bodyBrush = new LinearGradientBrush(highlightColor, shadowColor, 0);
            dc.DrawRectangle(bodyBrush, null, bodyRect);

            // Inner flow area
            var innerThickness = pipeThickness * 0.5;
            var innerX = pipeX + (pipeThickness - innerThickness) / 2;
            var innerRect = new Rect(innerX, flangeWidth + 2, innerThickness, h - flangeWidth * 2 - 4);
            dc.DrawRectangle(new SolidColorBrush(innerColor), null, innerRect);

            if (HasFlow)
            {
                var flowColor = Color.FromArgb(80, 100, 200, 255);
                for (int i = 0; i < 6; i++)
                {
                    var dy = flangeWidth + 10 + (h - flangeWidth * 2 - 20) * i / 5.0;
                    dc.DrawEllipse(new SolidColorBrush(flowColor), null,
                        new Point(w / 2, dy), 2, 2);
                }
            }
        }
    }
}
