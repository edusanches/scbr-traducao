#:property TargetFramework=net10.0-windows
#:property UseWPF=true
#:property PublishAot=false

// Gera os ícones do app e as imagens do pacote MSIX a partir do símbolo da Star Citizen Brasil (o mesmo de
// src/components/ui/symbol.tsx no site). Rode da raiz do repositório, quando o símbolo mudar:
//
//   dotnet run tools/gerar-icones.cs
//
// Saem src/TraducaoScbr/Recursos/app.ico (e app-alerta.ico, com o ponto de aviso), packaging/Assets/*.png e o logo
// da listagem da loja em docs/loja.

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

var root = Directory.GetCurrentDirectory();
if (!File.Exists(Path.Combine(root, "TraducaoScbr.slnx")))
{
    Console.Error.WriteLine("Rode da raiz do repositório (onde está o TraducaoScbr.slnx).");
    return 1;
}

var thread = new Thread(() => Generate(root));
thread.SetApartmentState(ApartmentState.STA);
thread.Start();
thread.Join();
return 0;

static void Generate(string root)
{
    var resources = Path.Combine(root, "src", "TraducaoScbr", "Recursos");
    var assets = Path.Combine(root, "packaging", "Assets");
    Directory.CreateDirectory(resources);
    Directory.CreateDirectory(assets);
    foreach (var old in Directory.EnumerateFiles(assets, "*.png"))
    {
        File.Delete(old);
    }

    var symbol = Symbol(alert: false);
    var alert = Symbol(alert: true);

    // O símbolo com a margem do SVG (viewBox 0 0 48 48) e sem margem (o quadro verde ocupa tudo), para os
    // tamanhos pequenos da bandeja ficarem legíveis.
    var withMargin = new Rect(0, 0, 48, 48);
    var fullBleed = new Rect(3, 3, 42, 42);

    WriteIco(Path.Combine(resources, "app.ico"), symbol, withMargin, fullBleed);
    WriteIco(Path.Combine(resources, "app-alerta.ico"), alert, withMargin, fullBleed);
    WritePng(Path.Combine(resources, "simbolo-256.png"), Render(symbol, 256, 256, withMargin));

    // O logo da listagem da loja (Partner Center > Listagem > Logotipos da loja, 1:1).
    var store = Path.Combine(root, "docs", "loja");
    Directory.CreateDirectory(store);
    WritePng(Path.Combine(store, "logo-300.png"), Render(symbol, 300, 300, withMargin));
    WritePng(Path.Combine(store, "logo-1080.png"), Render(symbol, 1080, 1080, withMargin));

    int[] scales = [100, 125, 150, 200, 400];
    foreach (var scale in scales)
    {
        double f = scale / 100.0;
        WritePng(Path.Combine(assets, $"Square44x44Logo.scale-{scale}.png"), Render(symbol, Px(44 * f), Px(44 * f), withMargin));
        WritePng(Path.Combine(assets, $"StoreLogo.scale-{scale}.png"), Render(symbol, Px(50 * f), Px(50 * f), withMargin));
        WritePng(Path.Combine(assets, $"Square150x150Logo.scale-{scale}.png"), Render(symbol, Px(150 * f), Px(150 * f), Tile(150, 150)));
        WritePng(Path.Combine(assets, $"Wide310x150Logo.scale-{scale}.png"), Render(symbol, Px(310 * f), Px(150 * f), Tile(310, 150)));
        WritePng(Path.Combine(assets, $"SmallTile.scale-{scale}.png"), Render(symbol, Px(71 * f), Px(71 * f), Tile(71, 71)));
        WritePng(Path.Combine(assets, $"LargeTile.scale-{scale}.png"), Render(symbol, Px(310 * f), Px(310 * f), Tile(310, 310)));
    }

    // Os tamanhos exatos da barra de tarefas, do menu Iniciar e do Explorer.
    foreach (var size in new[] { 16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256 })
    {
        var box = size <= 24 ? fullBleed : withMargin;
        var image = Render(symbol, size, size, box);
        WritePng(Path.Combine(assets, $"Square44x44Logo.targetsize-{size}.png"), image);
        WritePng(Path.Combine(assets, $"Square44x44Logo.targetsize-{size}_altform-unplated.png"), image);
        WritePng(Path.Combine(assets, $"Square44x44Logo.targetsize-{size}_altform-lightunplated.png"), image);
    }

    Console.WriteLine($"Ícones em {resources} e {assets}.");
}

static int Px(double value) => (int)Math.Round(value);

// O símbolo ocupa 60% da altura do bloco, centralizado, como os blocos do Windows pedem.
static Rect Tile(double width, double height)
{
    var units = 42 / 0.6 / height; // unidades do SVG por pixel
    var w = width * units;
    var h = height * units;
    return new Rect(24 - w / 2, 24 - h / 2, w, h);
}

static Drawing Symbol(bool alert)
{
    static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    var green = Brush("#009c3b");
    var yellow = Brush("#ffd43b");
    var blue = Brush("#4f8cff");
    var shade = Brush("#2b5fd6");
    var white = new Pen(Brushes.White, 2);
    var orbit = new RotateTransform(-16, 24, 24);

    var group = new DrawingGroup();
    using (var dc = group.Open())
    {
        dc.DrawRoundedRectangle(green, null, new Rect(3, 3, 42, 42), 9, 9);
        dc.DrawGeometry(yellow, new Pen(yellow, 1.2) { LineJoin = PenLineJoin.Round }, Geometry.Parse("M24,9.5 L43,24 L24,38.5 L5,24 Z"));
        dc.PushTransform(orbit);
        dc.DrawEllipse(null, white, new Point(24, 24), 17.5, 5);
        dc.Pop();
        dc.DrawEllipse(shade, null, new Point(24, 24), 9, 9);
        dc.DrawGeometry(blue, null, Geometry.Parse("M29.285,16.715 A9,9 0 0 0 16.715,29.285 A9,9 0 0 0 29.285,16.715 Z"));
        dc.PushTransform(orbit);
        dc.DrawGeometry(null, white, Geometry.Parse("M6.5,24 A17.5,5 0 0 0 41.5,24"));
        dc.Pop();
        if (alert)
        {
            // O ponto de aviso da bandeja: tradução nova ou jogo mais novo que a tradução.
            dc.DrawEllipse(Brush("#f2877e"), new Pen(Brush("#060912"), 3), new Point(37.5, 37.5), 8, 8);
        }
    }

    group.Freeze();
    return group;
}

static BitmapSource Render(Drawing drawing, int width, int height, Rect box)
{
    var visual = new DrawingVisual();
    using (var dc = visual.RenderOpen())
    {
        var scale = Math.Min(width / box.Width, height / box.Height);
        dc.PushTransform(new TranslateTransform((width - box.Width * scale) / 2 - box.X * scale, (height - box.Height * scale) / 2 - box.Y * scale));
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.DrawDrawing(drawing);
    }

    var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
    bitmap.Render(visual);
    bitmap.Freeze();
    return bitmap;
}

static byte[] Png(BitmapSource image)
{
    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(image));
    using var stream = new MemoryStream();
    encoder.Save(stream);
    return stream.ToArray();
}

static void WritePng(string path, BitmapSource image) => File.WriteAllBytes(path, Png(image));

// Um .ico com BMP de 32 bits nos tamanhos pequenos (o que o Windows mais entende) e PNG nos grandes.
static void WriteIco(string path, Drawing drawing, Rect withMargin, Rect fullBleed)
{
    int[] sizes = [16, 20, 24, 32, 40, 48, 64, 256];
    var images = sizes.Select(size =>
    {
        var bitmap = Render(drawing, size, size, size <= 24 ? fullBleed : withMargin);
        return size >= 64 ? Png(bitmap) : Dib(bitmap);
    }).ToList();

    using var stream = new FileStream(path, FileMode.Create);
    using var writer = new BinaryWriter(stream);
    writer.Write((ushort)0);
    writer.Write((ushort)1);
    writer.Write((ushort)sizes.Length);
    var offset = 6 + 16 * sizes.Length;
    for (var i = 0; i < sizes.Length; i++)
    {
        writer.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
        writer.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(images[i].Length);
        writer.Write(offset);
        offset += images[i].Length;
    }

    foreach (var image in images)
    {
        writer.Write(image);
    }
}

static byte[] Dib(BitmapSource image)
{
    var bgra = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
    int w = bgra.PixelWidth, h = bgra.PixelHeight;
    var pixels = new byte[w * h * 4];
    bgra.CopyPixels(pixels, w * 4, 0);
    var maskStride = (w + 31) / 32 * 4;

    using var stream = new MemoryStream();
    using var writer = new BinaryWriter(stream);
    writer.Write(40);
    writer.Write(w);
    writer.Write(h * 2);
    writer.Write((ushort)1);
    writer.Write((ushort)32);
    writer.Write(0);
    writer.Write(w * h * 4 + maskStride * h);
    writer.Write(0);
    writer.Write(0);
    writer.Write(0);
    writer.Write(0);
    for (var y = h - 1; y >= 0; y--)
    {
        writer.Write(pixels, y * w * 4, w * 4);
    }

    for (var y = h - 1; y >= 0; y--)
    {
        var row = new byte[maskStride];
        for (var x = 0; x < w; x++)
        {
            if (pixels[(y * w + x) * 4 + 3] == 0)
            {
                row[x / 8] |= (byte)(0x80 >> (x % 8));
            }
        }

        writer.Write(row);
    }

    writer.Flush();
    return stream.ToArray();
}
