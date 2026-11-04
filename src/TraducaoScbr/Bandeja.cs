using System.Drawing;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace TraducaoScbr;

/// <summary>
/// O ícone na bandeja, perto do relógio: clique abre a janela, o menu tem Abrir, Conferir agora e Sair. Os avisos
/// saem como notificação do Windows (o balão da bandeja vira notificação no Windows 10 e 11).
/// </summary>
public sealed class Bandeja : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly Icon _normal;
    private readonly Icon _alert;
    private readonly ContextMenuStrip _menu;

    public Bandeja()
    {
        _normal = LoadIcon("app.ico");
        _alert = LoadIcon("app-alerta.ico");

        _menu = new ContextMenuStrip
        {
            Renderer = new MenuEscuro(),
            ShowImageMargin = false,
            Font = new Font("Segoe UI", 9.5f),
        };
        _menu.Items.Add(Item("Abrir a Tradução SCBR", () => OpenRequested?.Invoke(), bold: true));
        _menu.Items.Add(Item("Conferir agora", () => CheckRequested?.Invoke()));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(Item("Sair", () => ExitRequested?.Invoke()));

        _icon = new NotifyIcon
        {
            Icon = _normal,
            Text = AppInfo.Name,
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                OpenRequested?.Invoke();
            }
        };
        _icon.BalloonTipClicked += (_, _) => OpenRequested?.Invoke();
    }

    public event Action? OpenRequested;

    public event Action? CheckRequested;

    public event Action? ExitRequested;

    /// <summary>O texto ao passar o mouse e o ponto de aviso no ícone.</summary>
    public void SetState(string tooltip, bool alert)
    {
        // O Windows corta a dica em 127 caracteres; o .NET recusa acima disso.
        _icon.Text = tooltip.Length > 120 ? tooltip[..120] + "…" : tooltip;
        _icon.Icon = alert ? _alert : _normal;
    }

    public void Notify(string title, string text, bool warning) =>
        _icon.ShowBalloonTip(10_000, title, text.Length > 250 ? text[..250] + "…" : text, warning ? ToolTipIcon.Warning : ToolTipIcon.Info);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        _normal.Dispose();
        _alert.Dispose();
    }

    private static ToolStripMenuItem Item(string text, Action action, bool bold = false)
    {
        var item = new ToolStripMenuItem(text) { Padding = new Padding(4, 3, 4, 3) };
        if (bold)
        {
            item.Font = new Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Bold);
        }

        item.Click += (_, _) => action();
        return item;
    }

    private static Icon LoadIcon(string name)
    {
        var resource = Application.GetResourceStream(new Uri($"pack://application:,,,/Recursos/{name}"))
            ?? throw new InvalidOperationException($"Falta o ícone {name}.");
        using var stream = resource.Stream;
        return new Icon(stream, SystemInformation.SmallIconSize);
    }

    /// <summary>O menu da bandeja nas cores do app (o padrão do WinForms é claro).</summary>
    private sealed class MenuEscuro : ToolStripProfessionalRenderer
    {
        private static readonly Color Fundo = Color.FromArgb(0x0B, 0x10, 0x20);
        private static readonly Color Destaque = Color.FromArgb(0x1A, 0x2C, 0x4A);
        private static readonly Color Linha = Color.FromArgb(0x2A, 0x33, 0x45);
        private static readonly Color Tinta = Color.FromArgb(0xED, 0xF2, 0xFB);

        public MenuEscuro()
            : base(new Cores())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Tinta;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var y = e.Item.Height / 2;
            using var pen = new Pen(Linha);
            e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
        }

        private sealed class Cores : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => Fundo;

            public override Color MenuBorder => Linha;

            public override Color MenuItemBorder => Destaque;

            public override Color MenuItemSelected => Destaque;

            public override Color MenuItemSelectedGradientBegin => Destaque;

            public override Color MenuItemSelectedGradientEnd => Destaque;

            public override Color ImageMarginGradientBegin => Fundo;

            public override Color ImageMarginGradientMiddle => Fundo;

            public override Color ImageMarginGradientEnd => Fundo;

            public override Color SeparatorDark => Linha;

            public override Color SeparatorLight => Fundo;
        }
    }
}
