using System.Windows;
using System.Windows.Media;

namespace TraducaoScbr;

/// <summary>
/// O fundo da janela: espaço azul-marinho com nebulosa, estrelas e a borda de um planeta, desenhado em código (no
/// espírito do launcher do jogo, sem usar as artes dele). As estrelas saem sempre no mesmo lugar.
/// </summary>
public sealed class FundoEspacial : FrameworkElement
{
    private static readonly (double X, double Y, double Size, double Alpha)[] Stars = MakeStars();

    public FundoEspacial()
    {
        // Sem isso, a nebulosa desenhada além da borda de cima cobria a barra do topo e pegava o clique dos botões.
        ClipToBounds = true;
        IsHitTestVisible = false;
    }

    private static readonly Brush Base = Frozen(new LinearGradientBrush(
        new GradientStopCollection
        {
            new(Color.FromRgb(0x0E, 0x22, 0x37), 0),
            new(Color.FromRgb(0x09, 0x16, 0x25), 0.55),
            new(Color.FromRgb(0x06, 0x0F, 0x1A), 1),
        },
        new Point(0, 0),
        new Point(0, 1)));

    private static readonly Brush NebulaBlue = Frozen(new RadialGradientBrush(
        new GradientStopCollection { new(Color.FromArgb(0x55, 0x2E, 0x6B, 0xC2), 0), new(Color.FromArgb(0, 0x2E, 0x6B, 0xC2), 1) }));

    private static readonly Brush NebulaGreen = Frozen(new RadialGradientBrush(
        new GradientStopCollection { new(Color.FromArgb(0x26, 0x22, 0xC9, 0x6B), 0), new(Color.FromArgb(0, 0x22, 0xC9, 0x6B), 1) }));

    private static readonly Brush Planet = Frozen(new RadialGradientBrush(
        new GradientStopCollection
        {
            new(Color.FromRgb(0x07, 0x12, 0x1E), 0.82),
            new(Color.FromRgb(0x12, 0x33, 0x55), 0.97),
            new(Color.FromArgb(0x80, 0x4F, 0x8C, 0xFF), 1),
        }));

    private static readonly Brush Atmosphere = Frozen(new RadialGradientBrush(
        new GradientStopCollection
        {
            new(Color.FromArgb(0, 0x4F, 0x8C, 0xFF), 0.88),
            new(Color.FromArgb(0x1C, 0x4F, 0x8C, 0xFF), 0.93),
            new(Color.FromArgb(0, 0x4F, 0x8C, 0xFF), 1),
        }));

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        dc.DrawRectangle(Base, null, new Rect(0, 0, w, h));
        dc.DrawEllipse(NebulaBlue, null, new Point(w * 0.72, h * 0.18), w * 0.55, h * 0.6);
        dc.DrawEllipse(NebulaGreen, null, new Point(w * 0.08, h * 0.95), w * 0.4, h * 0.45);

        foreach (var (x, y, size, alpha) in Stars)
        {
            var star = new SolidColorBrush(Color.FromArgb((byte)(alpha * 255), 0xDD, 0xE8, 0xFF));
            star.Freeze();
            dc.DrawEllipse(star, null, new Point(x * w, y * h), size, size);
        }

        // A borda de um planeta embaixo à direita, com o brilho da atmosfera.
        var radius = Math.Max(w, h) * 0.75;
        var center = new Point(w * 0.92, h + radius * 0.62);
        dc.DrawEllipse(Atmosphere, null, center, radius * 1.08, radius * 1.08);
        dc.DrawEllipse(Planet, null, center, radius, radius);
    }

    private static (double, double, double, double)[] MakeStars()
    {
        var random = new Random(4103);
        return Enumerable.Range(0, 260)
            .Select(_ =>
            {
                var bright = random.NextDouble() < 0.08;
                return (random.NextDouble(), random.NextDouble() * 0.85, bright ? 1.3 : 0.4 + random.NextDouble() * 0.6, bright ? 0.9 : 0.15 + random.NextDouble() * 0.45);
            })
            .ToArray();
    }

    private static Brush Frozen(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
