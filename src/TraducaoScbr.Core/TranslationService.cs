using TraducaoScbr.Core.Game;
using TraducaoScbr.Core.State;
using TraducaoScbr.Core.Translation;

namespace TraducaoScbr.Core;

/// <summary>O resultado de uma conferência: os avisos novos para a bandeja.</summary>
public sealed record CheckOutcome(IReadOnlyList<Notice> Notices);

/// <summary>
/// O coração do app: confere a tradução publicada, acha os canais, atualiza sozinho os que a pessoa quer e instala
/// ou remove a pedido. Uma operação de cada vez.
/// </summary>
public sealed class TranslationService
{
    private readonly TranslationApi _api;
    private readonly SettingsStore _store;
    private readonly Func<IReadOnlyList<string>, LocatorSources> _sources;
    private readonly string _cacheFolder;
    private readonly Action<string> _log;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public TranslationService(
        TranslationApi api,
        SettingsStore store,
        Func<IReadOnlyList<string>, LocatorSources> sources,
        string cacheFolder,
        Action<string>? log = null,
        Settings? settings = null)
    {
        _api = api;
        _store = store;
        _sources = sources;
        _cacheFolder = cacheFolder;
        _log = log ?? (_ => { });
        Settings = settings ?? store.Load();
    }

    public Settings Settings { get; }

    public bool FirstRun => _store.FirstRun;

    /// <summary>A tradução publicada na última conferência que deu certo.</summary>
    public Publication? Current { get; private set; }

    /// <summary>Se o site respondeu que ainda não há tradução publicada.</summary>
    public bool NothingPublished { get; private set; }

    /// <summary>O erro da última conferência, ou null quando deu certo.</summary>
    public string? ApiError { get; private set; }

    public DateTimeOffset? LastCheck { get; private set; }

    public IReadOnlyList<ChannelState> Channels { get; private set; } = [];

    /// <summary>Confere o site e os canais e atualiza sozinho o que for preciso.</summary>
    public async Task<CheckOutcome> CheckAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await FetchAsync(ct);
            await ScanAsync(ct);

            var updated = new List<GameChannel>();
            var failed = new List<(GameChannel, string)>();
            var todo = Reconciler.ToUpdate(Channels, Current, Settings.AutoUpdate);
            if (todo.Count > 0 && Current is { } publication)
            {
                try
                {
                    var file = await EnsureDownloadedAsync(publication, null, ct);
                    foreach (var state in todo)
                    {
                        try
                        {
                            await Task.Run(() => Installer.Install(state.Channel.Path, file, publication.Sha256), ct);
                            Remember(state.Channel.Path, wanted: true, publication.ToRecord());
                            updated.Add(state.Channel);
                            _log($"Atualizado sozinho: {state.Channel.Name} ({state.Channel.Path}) para {publication.Description}");
                        }
                        catch (InstallException e)
                        {
                            failed.Add((state.Channel, e.Message));
                            _log($"Falha ao atualizar {state.Channel.Path}: {e.Message}");
                        }
                    }
                }
                catch (ApiException e)
                {
                    failed.AddRange(todo.Select(s => (s.Channel, e.Message)));
                    _log($"Falha no download: {e.Message}");
                }

                await ScanAsync(ct);
            }

            var notices = Reconciler.Notices(Current, Settings.LastSeen, Channels, updated, failed, Settings.Notified);
            Settings.RememberNotified(notices.Select(n => n.Key));
            if (Current is not null)
            {
                Settings.LastSeen = Current.ToRecord();
            }

            Save();
            return new CheckOutcome(notices);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Confere só os canais (sem o site), depois de uma atualização do jogo, por exemplo.</summary>
    public async Task<CheckOutcome> RefreshChannelsAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await ScanAsync(ct);
            var notices = Reconciler.Notices(Current, Settings.LastSeen, Channels, [], [], Settings.Notified);
            Settings.RememberNotified(notices.Select(n => n.Key));
            Save();
            return new CheckOutcome(notices);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Instala (ou atualiza, ou volta a escolher) a tradução publicada num canal.</summary>
    public async Task InstallAsync(string channelPath, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            // Confere de novo, para instalar a mais nova. Sem internet, vale a da última conferência.
            await FetchAsync(ct);
            var publication = Current ?? throw new InstallException(
                ApiError ?? "Ainda não há tradução publicada no scbr.space.");
            var file = await EnsureDownloadedAsync(publication, progress, ct);
            await Task.Run(() => Installer.Install(channelPath, file, publication.Sha256), ct);
            Remember(channelPath, wanted: true, publication.ToRecord());
            _log($"Instalado: {channelPath} com {publication.Description}");
            await ScanAsync(ct);
            Save();
        }
        catch (ApiException e)
        {
            throw new InstallException(e.Message, e);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Tira a tradução de um canal. O app para de cuidar dele até a pessoa instalar de novo.</summary>
    public async Task RemoveAsync(string channelPath, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await Task.Run(() => Installer.Remove(channelPath), ct);
            Remember(channelPath, wanted: false, installed: null);
            _log($"Removido: {channelPath}");
            await ScanAsync(ct);
            Save();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Guarda uma pasta escolhida pela pessoa. Devolve quantos canais ela tem (0 = não é do jogo).</summary>
    public async Task<int> AddFolderAsync(string folder, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var channels = await Task.Run(() => GameLocator.ResolveFolder(folder), ct);
            if (channels.Count == 0)
            {
                return 0;
            }

            var normalized = GameLocator.Normalize(folder)!;
            if (!Settings.ExtraFolders.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                Settings.ExtraFolders.Add(normalized);
            }

            await ScanAsync(ct);
            Save();
            return channels.Count;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Save()
    {
        try
        {
            _store.Save(Settings);
        }
        catch (IOException e)
        {
            _log($"Não deu para gravar as configurações: {e.Message}");
        }
        catch (UnauthorizedAccessException e)
        {
            _log($"Não deu para gravar as configurações: {e.Message}");
        }
    }

    private async Task FetchAsync(CancellationToken ct)
    {
        try
        {
            Current = await _api.GetCurrentAsync(ct);
            NothingPublished = Current is null;
            ApiError = null;
            LastCheck = DateTimeOffset.Now;
        }
        catch (ApiException e)
        {
            // Fica a publicação da conferência anterior, se houver: o app segue funcionando sem internet.
            ApiError = e.Message;
            _log($"Conferência falhou: {e.Message}");
        }
    }

    private async Task ScanAsync(CancellationToken ct)
    {
        var current = Current;
        var extra = Settings.ExtraFolders.ToList();
        var states = await Task.Run(
            () => GameLocator.Find(_sources(extra))
                .Select(c => Reconciler.Evaluate(c, Installer.Inspect(c.Path), Settings.Channel(c.Path), current))
                .ToList(),
            ct);

        // Quem já tinha a tradução passa a ser cuidado pelo app (fica registrado como "quer").
        foreach (var state in states.Where(s => s.Wanted && Settings.Channel(s.Channel.Path) is null))
        {
            Settings.ChannelOrNew(state.Channel.Path).Wanted = true;
        }

        Channels = states;
    }

    private void Remember(string channelPath, bool wanted, PublicationRecord? installed)
    {
        var record = Settings.ChannelOrNew(GameLocator.Normalize(channelPath) ?? channelPath);
        record.Wanted = wanted;
        record.Installed = installed;
    }

    // O arquivo publicado fica guardado na pasta de dados do app (só o atual), para reinstalar sem baixar de novo.
    private async Task<string> EnsureDownloadedAsync(Publication publication, IProgress<double>? progress, CancellationToken ct)
    {
        Directory.CreateDirectory(_cacheFolder);
        var path = Path.Combine(_cacheFolder, $"global-{publication.Sha256}.ini");
        if (File.Exists(path))
        {
            var sha = await Task.Run(() => Installer.Sha256Of(path), ct);
            if (sha.Equals(publication.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }

        _log($"Baixando {publication.File} ({publication.Size} bytes)");
        await _api.DownloadAsync(publication, path, progress, ct);
        foreach (var old in Directory.EnumerateFiles(_cacheFolder, "global-*.ini"))
        {
            if (!string.Equals(old, path, StringComparison.OrdinalIgnoreCase))
            {
                TextFile.TryDelete(old);
            }
        }

        return path;
    }
}
