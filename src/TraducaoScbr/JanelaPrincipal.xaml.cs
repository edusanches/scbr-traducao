using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace TraducaoScbr;

/// <summary>A janela, no estilo do launcher do jogo, com a barra do topo do app.</summary>
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

    private MainViewModel ViewModel => _app.ViewModel;

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

    // Maximizada, a janela sem a moldura do Windows passa uns pixels da tela: a margem compensa.
    private void Janela_StateChanged(object? sender, EventArgs e) =>
        Raiz.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);

    private void Minimizar_Click(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void Fechar_Click(object sender, RoutedEventArgs e) => Close();

    private void Sair_Click(object sender, RoutedEventArgs e) => _app.ExitApp();

    private void Marca_Click(object sender, RoutedEventArgs e) => OpenMenu(Marca, Marca.ContextMenu);

    /// <summary>O seletor de canal, como o "Game Environment" do launcher: um menu com os canais achados.</summary>
    private void EscolherCanal_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { MinWidth = SeletorCanal.ActualWidth + EngrenagemCanal.ActualWidth };
        foreach (var channel in ViewModel.Channels)
        {
            var item = new MenuItem
            {
                Header = channel.MenuLabel,
                IsCheckable = true,
                IsChecked = channel.IsSelected,
            };
            item.Click += (_, _) => ViewModel.SelectedChannel = channel;
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Adicionar pasta do jogo…", AdicionarPasta_Click));
        OpenMenu(SeletorCanal, menu);
    }

    private void MenuCanal_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Item("Abrir a pasta do canal", AbrirPasta_Click, ViewModel.HasSelectedChannel));
        menu.Items.Add(Item("Adicionar pasta do jogo…", AdicionarPasta_Click));
        OpenMenu(EngrenagemCanal, menu);
    }

    private void SelecionarCanal_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ChannelViewModel channel)
        {
            ViewModel.SelectedChannel = channel;
        }
    }

    private async void Instalar_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedChannel is { } channel)
        {
            await _app.InstallAsync(channel);
        }
    }

    private async void Remover_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedChannel is not { } channel)
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
        if (ViewModel.SelectedChannel is { } channel)
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

    private void AbrirPagina_Click(object sender, RoutedEventArgs e) =>
        Open(ViewModel.Page?.AbsoluteUri ?? "https://scbr.space/traducao");

    private void AbrirPaginaTraducao_Click(object sender, RoutedEventArgs e) => Open("https://scbr.space/traducao");

    private void AbrirSite_Click(object sender, RoutedEventArgs e) => Open("https://scbr.space");

    private static MenuItem Item(string header, RoutedEventHandler click, bool enabled = true)
    {
        var item = new MenuItem { Header = header, IsEnabled = enabled };
        item.Click += click;
        return item;
    }

    private static void OpenMenu(FrameworkElement target, ContextMenu? menu)
    {
        if (menu is null)
        {
            return;
        }

        menu.PlacementTarget = target;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

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
