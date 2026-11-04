using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace TraducaoScbr;

public partial class JanelaPrincipal : Window
{
    private readonly App _app;
    private bool _closing;

    public JanelaPrincipal(App app)
    {
        _app = app;
        InitializeComponent();
        DataContext = app.ViewModel;
    }

    /// <summary>Fecha a janela de vez (no Sair do menu da bandeja).</summary>
    public void CloseForReal()
    {
        _closing = true;
        Close();
    }

    /// <summary>Grava a janela num PNG em escala 2x (para os prints da loja e do README).</summary>
    public void SaveImage(string file)
    {
        const double scale = 2;
        var width = (int)Math.Ceiling(Raiz.ActualWidth * scale);
        var height = (int)Math.Ceiling(Raiz.ActualHeight * scale);
        var bitmap = new RenderTargetBitmap(width, height, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        using (var dc = background.RenderOpen())
        {
            dc.DrawRectangle((Brush)FindResource("Superficie"), null, new Rect(0, 0, Raiz.ActualWidth, Raiz.ActualHeight));
        }

        bitmap.Render(background);
        bitmap.Render(Raiz);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        using var stream = File.Create(file);
        encoder.Save(stream);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Fechar a janela deixa o app na bandeja, conferindo a tradução.
        if (!_closing)
        {
            e.Cancel = true;
            Hide();
            _app.WindowHidden();
        }

        base.OnClosing(e);
    }

    private async void Instalar_Click(object sender, RoutedEventArgs e)
    {
        if (Channel(sender) is { } channel)
        {
            await _app.InstallAsync(channel);
        }
    }

    private async void Remover_Click(object sender, RoutedEventArgs e)
    {
        if (Channel(sender) is not { } channel)
        {
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"Tirar a tradução do {channel.Name}? O jogo volta ao inglês neste canal e o app para de atualizar a tradução nele.",
            "Remover a tradução",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes)
        {
            await _app.RemoveAsync(channel);
        }
    }

    private void AbrirPasta_Click(object sender, RoutedEventArgs e)
    {
        if (Channel(sender) is { } channel)
        {
            Open("explorer.exe", $"\"{channel.Path}\"");
        }
    }

    private async void AdicionarPasta_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Escolha a pasta do Star Citizen (a do canal, como LIVE, ou a StarCitizen)",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var found = await _app.AddFolderAsync(dialog.FolderName);
        if (found == 0)
        {
            MessageBox.Show(
                this,
                "Não achei o Star Citizen nessa pasta. Escolha a pasta do canal (a que tem o Data.p4k, como LIVE ou PTU) ou a StarCitizen, onde ficam os canais.",
                "Pasta do jogo",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private async void Conferir_Click(object sender, RoutedEventArgs e) => await _app.CheckAsync(userAsked: true);

    private void AbrirPagina_Click(object sender, RoutedEventArgs e)
    {
        if (_app.ViewModel.Page is { } page)
        {
            Open(page.AbsoluteUri);
        }
    }

    private void AbrirSite_Click(object sender, RoutedEventArgs e) => Open("https://scbr.space/traducao");

    private static ChannelViewModel? Channel(object sender) => (sender as FrameworkElement)?.DataContext as ChannelViewModel;

    private static void Open(string target, string? arguments = null)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { Arguments = arguments ?? "", UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Registro.Write($"Não deu para abrir {target}: {ex.Message}");
        }
    }
}
