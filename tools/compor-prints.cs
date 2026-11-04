#:property TargetFramework=net10.0-windows
#:property UseWPF=true
#:property PublishAot=false

// Monta as capturas da Microsoft Store (1920 x 1080) a partir das capturas da janela que o app grava com
// "--captura" (veja docs/loja.md). Rode da raiz do repositório:
//
//   dotnet run tools/compor-prints.cs -- <captura.png> <saida.png> ["<título>" "<texto>"]
//
// Sem o título, a janela fica centralizada no fundo (é o caso da janela larga, no estilo do launcher).

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

if (args.Length < 2)
{
    Console.Error.WriteLine("Uso: dotnet run tools/compor-prints.cs -- <captura.png> <saida.png> [\"<título>\" \"<texto>\"]");
    return 1;
}

var thread = new Thread(() => Compose(args[0], args[1], args.Length > 2 ? args[2] : null, args.Length > 3 ? args[3] : ""));
thread.SetApartmentState(ApartmentState.STA);
thread.Start();
thread.Join();
return 0;

static void Compose(string capture, string output, string? title, string text)
{
    const int width = 1920, height = 1080;
    var shot = new BitmapImage();
    shot.BeginInit();
    shot.CacheOption = BitmapCacheOption.OnLoad;
    shot.UriSource = new Uri(Path.GetFullPath(capture));
    shot.EndInit();

    var visual = new DrawingVisual();
    using (var dc = visual.RenderOpen())
    {
        // O fundo do site: espaço profundo com o brilho azul e o verde.
        dc.DrawRectangle(Brush("#060912"), null, new Rect(0, 0, width, height));
        dc.DrawRectangle(Glow("#384F8CFF", 0.12, 0.0, 0.9, 0.9), null, new Rect(0, 0, width, height));
        dc.DrawRectangle(Glow("#1A22C96B", 0.95, 1.0, 0.7, 0.6), null, new Rect(0, 0, width, height));

        // A janela à direita (ou no meio, sem título), com a borda e a sombra dos painéis.
        var scale = Math.Min((width - 120.0) / shot.PixelWidth, (height - 120.0) / shot.PixelHeight);
        var shotWidth = shot.PixelWidth * scale;
        var shotHeight = shot.PixelHeight * scale;
        var frame = title is null
            ? new Rect((width - shotWidth) / 2, (height - shotHeight) / 2, shotWidth, shotHeight)
            : new Rect(width - shotWidth - 110, 60, shotWidth, shotHeight);
        dc.DrawRoundedRectangle(Brush("#80000000"), null, new Rect(frame.X + 6, frame.Y + 14, frame.Width, frame.Height), 18, 18);
        dc.PushClip(new RectangleGeometry(frame, 14, 14));
        dc.DrawImage(shot, frame);
        dc.Pop();
        dc.DrawRoundedRectangle(null, new Pen(Brush("#5794A3B8"), 1.5), frame, 14, 14);

        // O texto à esquerda.
        if (title is not null)
        {
            var left = 110.0;
            var textWidth = frame.X - left - 90;
            var typeface = new Typeface(new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            var body = new Typeface(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var heading = Text(title, typeface, 64, "#EDF2FB", textWidth);
            var paragraph = Text(text, body, 30, "#97A2B6", textWidth);
            var top = (height - heading.Height - 36 - paragraph.Height) / 2;
            dc.DrawText(heading, new Point(left, top));
            dc.DrawText(paragraph, new Point(left, top + heading.Height + 36));
        }
    }

    var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
    bitmap.Render(visual);
    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(bitmap));
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
    using var stream = File.Create(output);
    encoder.Save(stream);
    Console.WriteLine($"{output}: {width} x {height}");
}

static FormattedText Text(string text, Typeface typeface, double size, string color, double maxWidth) =>
    new(text, System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), FlowDirection.LeftToRight, typeface, size, Brush(color), 1.0)
    {
        MaxTextWidth = maxWidth,
        LineHeight = size * 1.25,
    };

static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));

static RadialGradientBrush Glow(string hex, double x, double y, double rx, double ry) =>
    new()
    {
        Center = new Point(x, y),
        GradientOrigin = new Point(x, y),
        RadiusX = rx,
        RadiusY = ry,
        GradientStops = { new GradientStop((Color)ColorConverter.ConvertFromString(hex), 0), new GradientStop(Colors.Transparent, 1) },
    };
