using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TraducaoScbr;

/// <summary>
/// Um painel com o canto de baixo à direita cortado em diagonal, como os botões e os cartões do launcher do jogo.
/// Desenha o fundo e a borda e dá o espaço do <see cref="Padding"/> ao conteúdo.
/// </summary>
public sealed class Chanfro : Decorator
{
    public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(
        nameof(Background), typeof(Brush), typeof(Chanfro), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty = DependencyProperty.Register(
        nameof(BorderBrush), typeof(Brush), typeof(Chanfro), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderThicknessProperty = DependencyProperty.Register(
        nameof(BorderThickness), typeof(double), typeof(Chanfro),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty CutProperty = DependencyProperty.Register(
        nameof(Cut), typeof(double), typeof(Chanfro), new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PaddingProperty = DependencyProperty.Register(
        nameof(Padding), typeof(Thickness), typeof(Chanfro), new FrameworkPropertyMetadata(default(Thickness), FrameworkPropertyMetadataOptions.AffectsMeasure));

    public Brush? Background
    {
        get => (Brush?)GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Brush? BorderBrush
    {
        get => (Brush?)GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public double BorderThickness
    {
        get => (double)GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    /// <summary>O tamanho do corte, em pixels.</summary>
    public double Cut
    {
        get => (double)GetValue(CutProperty);
        set => SetValue(CutProperty, value);
    }

    public Thickness Padding
    {
        get => (Thickness)GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    protected override Size MeasureOverride(Size constraint)
    {
        var (horizontal, vertical) = Spacing();
        if (Child is null)
        {
            return new Size(horizontal, vertical);
        }

        Child.Measure(new Size(Math.Max(0, constraint.Width - horizontal), Math.Max(0, constraint.Height - vertical)));
        return new Size(Child.DesiredSize.Width + horizontal, Child.DesiredSize.Height + vertical);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        var (horizontal, vertical) = Spacing();
        Child?.Arrange(new Rect(
            Padding.Left + BorderThickness,
            Padding.Top + BorderThickness,
            Math.Max(0, arrangeSize.Width - horizontal),
            Math.Max(0, arrangeSize.Height - vertical)));
        return arrangeSize;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var border = BorderThickness;
        var half = border / 2;
        var cut = Math.Min(Cut, Math.Min(width, height) / 2);
        var shape = new StreamGeometry();
        using (var context = shape.Open())
        {
            context.BeginFigure(new Point(half, half), isFilled: true, isClosed: true);
            context.LineTo(new Point(width - half, half), true, false);
            context.LineTo(new Point(width - half, height - cut), true, false);
            context.LineTo(new Point(width - cut, height - half), true, false);
            context.LineTo(new Point(half, height - half), true, false);
        }

        shape.Freeze();
        var pen = BorderBrush is { } brush && border > 0 ? new Pen(brush, border) { LineJoin = PenLineJoin.Miter } : null;
        dc.DrawGeometry(Background, pen, shape);
    }

    private (double Horizontal, double Vertical) Spacing() =>
        (Padding.Left + Padding.Right + 2 * BorderThickness, Padding.Top + Padding.Bottom + 2 * BorderThickness);
}
