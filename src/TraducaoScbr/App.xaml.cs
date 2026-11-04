using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using TraducaoScbr.Core;
using TraducaoScbr.Core.Game;
using TraducaoScbr.Core.State;
using TraducaoScbr.Core.Translation;
using StartupTaskState = Windows.ApplicationModel.StartupTaskState;

namespace TraducaoScbr;

/// <summary>
/// O app: uma instância só, o ícone na bandeja, a conferência ao abrir e de tempos em tempos, e a janela quando a
/// pessoa pede. Fechar a janela deixa o app na bandeja; Sair (no menu do ícone) fecha de vez.
/// </summary>
public partial class App : Application
{
    private const string InstanceName = @"Local\TraducaoSCBR.Instancia";
    private const string ShowEventName = @"Local\TraducaoSCBR.Mostrar";

    private Mutex? _instance;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _showWait;
    private Bandeja? _tray;
    private JanelaPrincipal? _window;
    private DispatcherTimer? _timer;
    private DispatcherTimer? _watchDebounce;
    private readonly List<FileSystemWatcher> _watchers = [];
    private HttpClient? _http;

    internal TranslationService Service { get; private set; } = null!;

    internal MainViewModel ViewModel { get; } = new();

    internal AppOptions Options { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Options = AppOptions.Parse(e.Args);

        // Uma instância só: a segunda pede para a primeira mostrar a janela e sai.
        _instance = new Mutex(initiallyOwned: true, InstanceName, out var first);
        if (!first && Options.Capture is null)
        {
            if (EventWaitHandle.TryOpenExisting(ShowEventName, out var other))
            {
                other.Set();
                other.Dispose();
            }

            Shutdown();
            return;
        }

        if (first)
        {
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
            _showWait = ThreadPool.RegisterWaitForSingleObject(
                _showEvent, (_, _) => Dispatcher.BeginInvoke(ShowWindow), null, Timeout.Infinite, executeOnlyOnce: false);
        }

        var dataFolder = Options.DataFolder ?? AppInfo.DefaultDataFolder;
        Registro.Start(dataFolder);
        // Um erro inesperado vai para o registro e o app segue na bandeja.
        DispatcherUnhandledException += (_, args) =>
        {
            Registro.Write($"Erro: {args.Exception}");
            args.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Registro.Write($"Erro numa tarefa: {args.Exception}");
            args.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Registro.Write($"Erro fatal: {args.ExceptionObject}");

        var store = new SettingsStore(Path.Combine(dataFolder, "settings.json"));
        var settings = store.Load();
        var api = Options.Api
            ?? (settings.ApiBaseUrl is { } custom && Uri.TryCreate(custom, UriKind.Absolute, out var fromSettings) ? fromSettings : null)
            ?? TranslationApi.DefaultBase;
        _http = TranslationApi.CreateHttpClient(AppInfo.Version);
        var only = Options.OnlyFolders;
        Service = new TranslationService(
            new TranslationApi(_http, api),
            store,
            extra => only is not null ? new LocatorSources([], [], [.. only, .. extra], AutoDetect: false) : GameLocator.DefaultSources(extra),
            Path.Combine(dataFolder, "cache"),
            Registro.Write,
            settings);
        Registro.Write($"API: {api}");
        if (Options.Tab is { } tab)
        {
            ViewModel.ShowTab(tab);
        }

        if (Options.Capture is { } capture)
        {
            await CaptureAsync(capture);
            return;
        }

        ViewModel.AutoUpdateChanged += SetAutoUpdate;
        ViewModel.StartWithWindowsChanged += value => _ = SetStartWithWindowsAsync(value);

        _tray = new Bandeja();
        _tray.OpenRequested += ShowWindow;
        _tray.CheckRequested += () => _ = CheckAsync(userAsked: true);
        _tray.ExitRequested += ExitApp;

        var atLogin = Options.Tray || AppInfo.LaunchedAtLogin();
        if (!atLogin || Service.FirstRun)
        {
            ShowWindow();
        }

        if (!Service.Settings.StartupOffered && AppInfo.IsPackaged)
        {
            // Na primeira vez o app liga o início com o Windows, que é o que mantém a tradução em dia. A pessoa
            // desliga na janela (ou nas Configurações do Windows).
            var state = await InicioComWindows.EnableAsync();
            Registro.Write($"Início com o Windows: {state}");
            Service.Settings.StartupOffered = true;
            Service.Save();
        }

        await RefreshStartupAsync();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromHours(Service.Settings.CheckIntervalHours) };
        _timer.Tick += (_, _) => _ = CheckAsync(userAsked: false);
        _timer.Start();
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;

        // No login, espera o Windows assentar antes de usar a rede e o disco.
        if (atLogin)
        {
            await Task.Delay(TimeSpan.FromSeconds(45));
        }

        await CheckAsync(userAsked: false);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
        foreach (var watcher in _watchers)
        {
            watcher.Dispose();
        }

        _tray?.Dispose();
        _showWait?.Unregister(null);
        _showEvent?.Dispose();
        _http?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }

    internal void ShowWindow()
    {
        if (_window is null)
        {
            _window = new JanelaPrincipal(this);
            _window.Closed += (_, _) => _window = null;
        }

        _window.Show();
        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Activate();
        // Traz para a frente mesmo quando outro app está em foco.
        _window.Topmost = true;
        _window.Topmost = false;
        _window.Focus();
    }

    /// <summary>A janela foi fechada pela pessoa: o app segue na bandeja.</summary>
    internal void WindowHidden()
    {
        if (!Service.Settings.TrayHintShown)
        {
            Service.Settings.TrayHintShown = true;
            Service.Save();
            Notify(
                "A Tradução SCBR continua na bandeja",
                "O app confere a tradução de tempos em tempos. Para abrir, clique no ícone perto do relógio; para fechar de vez, use Sair no menu dele.",
                warning: false,
                key: "bandeja");
        }
    }

    internal void ExitApp()
    {
        _window?.CloseForReal();
        Shutdown();
    }

    /// <summary>Confere o site e os canais e atualiza a tela, a bandeja e os avisos.</summary>
    internal async Task CheckAsync(bool userAsked)
    {
        if (ViewModel.Busy && !userAsked)
        {
            return;
        }

        ViewModel.Busy = true;
        ViewModel.BusyText = "Conferindo a tradução…";
        try
        {
            var outcome = await Service.CheckAsync();
            Show(outcome);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Registro.Write($"Conferência: {e}");
        }
        finally
        {
            ViewModel.Busy = false;
            ViewModel.BusyText = null;
            UpdateViews();
        }
    }

    /// <summary>Instala, atualiza ou ativa a tradução num canal.</summary>
    internal async Task InstallAsync(ChannelViewModel channel)
    {
        ViewModel.Busy = true;
        channel.Error = null;
        channel.Progress = "Preparando…";
        try
        {
            var progress = new Progress<double>(p => channel.Progress = $"Baixando a tradução… {p:P0}");
            await Service.InstallAsync(channel.Path, progress);
        }
        catch (Exception e) when (e is InstallException or IOException or UnauthorizedAccessException)
        {
            channel.Error = e is InstallException ? e.Message : $"Não deu para instalar: {e.Message}";
            Registro.Write($"Instalar {channel.Path}: {e}");
        }
        finally
        {
            channel.Progress = null;
            ViewModel.Busy = false;
            UpdateViews();
        }
    }

    internal async Task RemoveAsync(ChannelViewModel channel)
    {
        ViewModel.Busy = true;
        channel.Error = null;
        channel.Progress = "Removendo…";
        try
        {
            await Service.RemoveAsync(channel.Path);
        }
        catch (Exception e) when (e is InstallException or IOException or UnauthorizedAccessException)
        {
            channel.Error = e is InstallException ? e.Message : $"Não deu para remover: {e.Message}";
            Registro.Write($"Remover {channel.Path}: {e}");
        }
        finally
        {
            channel.Progress = null;
            ViewModel.Busy = false;
            UpdateViews();
        }
    }

    /// <summary>Guarda uma pasta escolhida pela pessoa. Devolve quantos canais ela tem.</summary>
    internal async Task<int> AddFolderAsync(string folder)
    {
        ViewModel.Busy = true;
        try
        {
            return await Service.AddFolderAsync(folder);
        }
        finally
        {
            ViewModel.Busy = false;
            UpdateViews();
        }
    }

    internal void SetAutoUpdate(bool value)
    {
        if (Service.Settings.AutoUpdate == value)
        {
            return;
        }

        Service.Settings.AutoUpdate = value;
        Service.Save();
        Registro.Write($"Atualizar sozinho: {value}");
        if (value)
        {
            _ = CheckAsync(userAsked: true);
        }
    }

    internal async Task SetStartWithWindowsAsync(bool value)
    {
        var state = value ? await InicioComWindows.EnableAsync() : await InicioComWindows.DisableAsync();
        Registro.Write($"Início com o Windows: {state}");
        await RefreshStartupAsync();
    }

    private async Task RefreshStartupAsync()
    {
        var state = await InicioComWindows.GetStateAsync();
        ViewModel.ShowStartup(
            available: AppInfo.IsPackaged && state is not null
                && state is not (StartupTaskState.DisabledByPolicy or StartupTaskState.EnabledByPolicy),
            on: InicioComWindows.IsOn(state),
            note: InicioComWindows.Note(state));
    }

    private void Show(CheckOutcome outcome)
    {
        if (outcome.Notices.Count == 0 || _tray is null)
        {
            return;
        }

        var first = outcome.Notices.OrderByDescending(n => n.Kind).First();
        var more = outcome.Notices.Count - 1;
        var text = more > 0 ? $"{first.Text} (Mais {more} {(more == 1 ? "aviso" : "avisos")} no app.)" : first.Text;
        Notify(first.Title, text, first.Kind == NoticeKind.Warning, first.Key);
        foreach (var notice in outcome.Notices)
        {
            Registro.Write($"Aviso: {notice.Title}: {notice.Text}");
        }
    }

    private void Notify(string title, string text, bool warning, string key)
    {
        if (!Notificacoes.TryShow(title, text, key))
        {
            _tray?.Notify(title, text, warning);
        }

        Registro.Write($"Notificações do app na central: {Notificacoes.InCenter()}");
    }

    private void UpdateViews()
    {
        ViewModel.Update(Service);
        UpdateTray();
        WatchChannels();
    }

    private void UpdateTray()
    {
        if (_tray is null)
        {
            return;
        }

        // O mesmo estado da barra do topo da janela.
        _tray.SetState($"{AppInfo.Name}: {ViewModel.OverallText.ToLowerInvariant()}", ViewModel.OverallAlert);
    }

    // Quando o launcher atualiza o jogo, o build_manifest.id muda: o app confere os canais na hora (sem o site),
    // para avisar se o jogo passou da tradução.
    private void WatchChannels()
    {
        var folders = Service.Channels.Select(c => c.Channel.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var watcher in _watchers.Where(w => !folders.Contains(w.Path)).ToList())
        {
            watcher.Dispose();
            _watchers.Remove(watcher);
        }

        foreach (var folder in folders.Where(f => !_watchers.Any(w => string.Equals(w.Path, f, StringComparison.OrdinalIgnoreCase))))
        {
            try
            {
                var watcher = new FileSystemWatcher(folder, BuildManifest.FileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                };
                watcher.Changed += OnManifestChanged;
                watcher.Created += OnManifestChanged;
                watcher.Renamed += OnManifestChanged;
                watcher.EnableRaisingEvents = true;
                _watchers.Add(watcher);
            }
            catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException)
            {
                Registro.Write($"Sem vigiar {folder}: {e.Message}");
            }
        }
    }

    private void OnManifestChanged(object sender, FileSystemEventArgs e) => Dispatcher.BeginInvoke(() =>
    {
        _watchDebounce ??= new DispatcherTimer(TimeSpan.FromSeconds(15), DispatcherPriority.Background, async (_, _) =>
        {
            _watchDebounce!.Stop();
            if (ViewModel.Busy)
            {
                _watchDebounce.Start();
                return;
            }

            Registro.Write("O jogo mudou de build: conferindo os canais.");
            Show(await Service.RefreshChannelsAsync());
            UpdateViews();
        }, Dispatcher);
        _watchDebounce.Stop();
        _watchDebounce.Start();
    });

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            Dispatcher.BeginInvoke(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30));
                await CheckAsync(userAsked: false);
            });
        }
    }

    private void OnNetworkChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        if (e.IsAvailable)
        {
            Dispatcher.BeginInvoke(async () =>
            {
                if (Service.ApiError is not null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));
                    await CheckAsync(userAsked: false);
                }
            });
        }
    }

    // Para os prints da loja e do README: abre a janela fora da tela, confere, grava a imagem e fecha.
    private async Task CaptureAsync(string file)
    {
        await CheckAsync(userAsked: false);
        var window = new JanelaPrincipal(this) { Left = -20000, Top = -20000, ShowActivated = false };
        window.Show();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        window.SaveImage(file);
        Registro.Write($"Imagem da janela em {file}");
        window.CloseForReal();
        Shutdown();
    }
}
