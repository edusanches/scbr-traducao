using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using TraducaoScbr.Core;
using TraducaoScbr.Core.State;
using TraducaoScbr.Core.Translation;

namespace TraducaoScbr;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Notify(name!);
        return true;
    }

    protected void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Os textos da tela, todos em português e com datas do Brasil, qualquer que seja o idioma do Windows.</summary>
public static class Texts
{
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string When(DateTimeOffset moment)
    {
        var local = moment.ToLocalTime();
        var today = DateTime.Today;
        if (local.Date == today)
        {
            return string.Create(PtBr, $"hoje às {local:HH:mm}");
        }

        if (local.Date == today.AddDays(-1))
        {
            return string.Create(PtBr, $"ontem às {local:HH:mm}");
        }

        return string.Create(PtBr, $"em {local:dd/MM/yyyy} às {local:HH:mm}");
    }

    public static string Publication(PublicationRecord record) => record.Description;
}

/// <summary>Um canal na tela.</summary>
public sealed class ChannelViewModel : Observable
{
    private string? _error;
    private string? _progress;

    public ChannelViewModel(ChannelState state)
    {
        State = state;
        var channel = state.Channel;
        Name = channel.Name;
        Path = channel.Path;
        GameBuild = channel.Build is { } build ? $"Jogo no build {build.ChangeNumber}" : "Build do jogo desconhecido";

        (StatusText, StatusBrush, PrimaryAction) = state.Status switch
        {
            ChannelStatus.NotInstalled => ("Sem a tradução", "TintaSuave", "Instalar"),
            ChannelStatus.UpToDate => ("Tradução em dia", "Positivo", null),
            ChannelStatus.Outdated => ("Tradução desatualizada", "Aviso", "Atualizar"),
            ChannelStatus.Inactive => ("Tradução instalada, mas desligada no user.cfg", "Aviso", "Ativar"),
            _ => ("Tradução instalada", "TintaSuave", null),
        };

        if (state.Inspection.Installed)
        {
            InstalledText = state.InstalledPublication is { } installed
                ? $"Instalada: {Texts.Publication(installed)}"
                : "Instalada: outra versão (de fora do app ou antiga)";
        }

        CanRemove = state.Inspection.Installed || state.Inspection.LanguageActive;
    }

    public ChannelState State { get; }

    public string Name { get; }

    public string Path { get; }

    public string GameBuild { get; }

    public string StatusText { get; }

    /// <summary>A chave do pincel no Tema.xaml.</summary>
    public string StatusBrush { get; }

    public Brush StatusColor => (Brush)System.Windows.Application.Current.FindResource(StatusBrush);

    public string? PrimaryAction { get; }

    public bool HasPrimaryAction => PrimaryAction is not null;

    // Os nomes dos botões para leitores de tela (e para os testes de interface): "Instalar a tradução no LIVE".
    public string PrimaryActionName => $"{PrimaryAction} a tradução no {Name}";

    public string RemoveName => $"Remover a tradução do {Name}";

    public string OpenFolderName => $"Abrir a pasta do {Name}";

    public string? InstalledText { get; }

    public bool CanRemove { get; }

    public string? AheadText { get; set; }

    public bool HasAhead => AheadText is not null;

    public string? Error
    {
        get => _error;
        set
        {
            if (Set(ref _error, value))
            {
                Notify(nameof(HasError));
            }
        }
    }

    public bool HasError => _error is not null;

    public string? Progress
    {
        get => _progress;
        set
        {
            if (Set(ref _progress, value))
            {
                Notify(nameof(HasProgress));
            }
        }
    }

    public bool HasProgress => _progress is not null;
}

/// <summary>A janela principal.</summary>
public sealed class MainViewModel : Observable
{
    private string _publicationTitle = "Conferindo o scbr.space…";
    private string? _publicationBuild;
    private string? _publicationDate;
    private Uri? _page;
    private string? _checkText;
    private bool _checkIsError;
    private bool _busy;
    private string? _busyText;
    private bool _autoUpdate;
    private bool _startWithWindows;
    private bool _startupAvailable;
    private string? _startupNote;
    private bool _scanned;
    private bool _syncing;

    public ObservableCollection<ChannelViewModel> Channels { get; } = [];

    public string PublicationTitle { get => _publicationTitle; private set => Set(ref _publicationTitle, value); }

    public string? PublicationBuild { get => _publicationBuild; private set => Set(ref _publicationBuild, value); }

    public string? PublicationDate { get => _publicationDate; private set => Set(ref _publicationDate, value); }

    public Uri? Page
    {
        get => _page;
        private set
        {
            if (Set(ref _page, value))
            {
                Notify(nameof(HasPage));
            }
        }
    }

    public bool HasPage => _page is not null;

    public string? CheckText { get => _checkText; private set => Set(ref _checkText, value); }

    public bool CheckIsError
    {
        get => _checkIsError;
        private set
        {
            if (Set(ref _checkIsError, value))
            {
                Notify(nameof(CheckColor));
            }
        }
    }

    public Brush CheckColor => (Brush)System.Windows.Application.Current.FindResource(_checkIsError ? "Aviso" : "TintaSuave");

    public bool Busy
    {
        get => _busy;
        set
        {
            if (Set(ref _busy, value))
            {
                Notify(nameof(Idle));
            }
        }
    }

    public bool Idle => !_busy;

    public string? BusyText { get => _busyText; set => Set(ref _busyText, value); }

    /// <summary>A pessoa mudou "Atualizar sozinho" (pelo mouse, teclado ou leitor de tela).</summary>
    public event Action<bool>? AutoUpdateChanged;

    /// <summary>A pessoa mudou "Iniciar com o Windows".</summary>
    public event Action<bool>? StartWithWindowsChanged;

    public bool AutoUpdate
    {
        get => _autoUpdate;
        set
        {
            if (Set(ref _autoUpdate, value) && !_syncing)
            {
                AutoUpdateChanged?.Invoke(value);
            }
        }
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (Set(ref _startWithWindows, value) && !_syncing)
            {
                StartWithWindowsChanged?.Invoke(value);
            }
        }
    }

    /// <summary>Mostra o estado do início com o Windows sem tratar como escolha da pessoa.</summary>
    public void ShowStartup(bool available, bool on, string? note)
    {
        _syncing = true;
        StartupAvailable = available;
        StartWithWindows = on;
        StartupNote = note;
        _syncing = false;
    }

    public bool StartupAvailable { get => _startupAvailable; set => Set(ref _startupAvailable, value); }

    public string? StartupNote
    {
        get => _startupNote;
        set
        {
            if (Set(ref _startupNote, value))
            {
                Notify(nameof(HasStartupNote));
            }
        }
    }

    public bool HasStartupNote => _startupNote is not null;

    public bool NoChannels => _scanned && Channels.Count == 0;

    public bool HasChannels => Channels.Count > 0;

    public string AppVersion { get; } = $"Versão {AppInfo.Version}";

    /// <summary>Copia o estado do serviço para a tela.</summary>
    public void Update(TranslationService service)
    {
        var current = service.Current;
        if (current is not null)
        {
            PublicationTitle = current.Label;
            PublicationBuild = current.Build is { } build ? $"build {build}" : null;
            PublicationDate = current.PublishedAt is { } at ? $"Publicada {Texts.When(at)}" : null;
            Page = current.Page;
        }
        else if (service.NothingPublished)
        {
            PublicationTitle = "Ainda não há tradução publicada";
            PublicationBuild = null;
            PublicationDate = "Quando a equipe de tradução publicar, o app avisa e instala.";
            Page = null;
        }
        else if (service.ApiError is not null)
        {
            PublicationTitle = "Sem resposta do scbr.space";
            PublicationBuild = null;
            PublicationDate = null;
        }

        if (service.ApiError is { } error)
        {
            CheckText = $"{error} O app tenta de novo a cada {service.Settings.CheckIntervalHours} h.";
            CheckIsError = true;
        }
        else if (service.LastCheck is { } last)
        {
            CheckText = $"Conferido {Texts.When(last)}. O app confere de novo a cada {service.Settings.CheckIntervalHours} h.";
            CheckIsError = false;
        }

        // O erro de uma instalação ou remoção fica na tela enquanto o canal continua no mesmo estado.
        var errors = Channels
            .Where(c => c.Error is not null)
            .ToDictionary(c => c.Path, c => (c.State.Status, c.Error), StringComparer.OrdinalIgnoreCase);
        Channels.Clear();
        foreach (var state in service.Channels)
        {
            var vm = new ChannelViewModel(state);
            if (state.GameAhead && current is not null)
            {
                vm.AheadText = $"O jogo está no build {state.Channel.Build!.ChangeNumber}, mais novo que o da tradução publicada ({current.Build}). Os textos novos aparecem em inglês até a próxima tradução.";
            }

            if (errors.TryGetValue(vm.Path, out var previous) && previous.Status == state.Status)
            {
                vm.Error = previous.Error;
            }

            Channels.Add(vm);
        }

        _scanned = true;
        _syncing = true;
        AutoUpdate = service.Settings.AutoUpdate;
        _syncing = false;
        Notify(nameof(NoChannels));
        Notify(nameof(HasChannels));
    }
}
