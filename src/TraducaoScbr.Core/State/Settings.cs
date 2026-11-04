using System.Text.Json;
using System.Text.Json.Serialization;
using TraducaoScbr.Core.Translation;

namespace TraducaoScbr.Core.State;

/// <summary>O que o app guarda no PC da pessoa (settings.json, na pasta de dados do app).</summary>
public sealed class Settings
{
    /// <summary>Atualizar sozinho os canais com a tradução quando sair uma nova.</summary>
    public bool AutoUpdate { get; set; } = true;

    /// <summary>De quantas em quantas horas conferir a tradução publicada.</summary>
    public int CheckIntervalHours { get; set; } = 3;

    /// <summary>As pastas do jogo que a pessoa escolheu, além das achadas sozinhas.</summary>
    public List<string> ExtraFolders { get; set; } = [];

    /// <summary>O que o app sabe de cada canal, pelo caminho da pasta.</summary>
    public Dictionary<string, ChannelRecord> Channels { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A última publicação vista, para saber quando sai uma nova.</summary>
    public PublicationRecord? LastSeen { get; set; }

    /// <summary>Os avisos já mostrados, para não repetir.</summary>
    public List<string> Notified { get; set; } = [];

    /// <summary>Se o app já ligou o início com o Windows na primeira vez (depois disso, a escolha é da pessoa).</summary>
    public bool StartupOffered { get; set; }

    /// <summary>Se o aviso de que o app continua na bandeja já apareceu.</summary>
    public bool TrayHintShown { get; set; }

    /// <summary>Outro endereço da API, só para testes com o mock.</summary>
    public string? ApiBaseUrl { get; set; }

    public ChannelRecord? Channel(string path) => Channels.GetValueOrDefault(path);

    public ChannelRecord ChannelOrNew(string path)
    {
        if (!Channels.TryGetValue(path, out var record))
        {
            record = new ChannelRecord();
            Channels[path] = record;
        }

        return record;
    }

    public void RememberNotified(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            if (!Notified.Contains(key))
            {
                Notified.Add(key);
            }
        }

        if (Notified.Count > 200)
        {
            Notified.RemoveRange(0, Notified.Count - 200);
        }
    }
}

/// <summary>O que o app sabe de um canal.</summary>
public sealed class ChannelRecord
{
    /// <summary>Se a pessoa quer a tradução neste canal (instalou pelo app, ou já tinha e não removeu).</summary>
    public bool Wanted { get; set; }

    /// <summary>A publicação instalada pelo app neste canal.</summary>
    public PublicationRecord? Installed { get; set; }
}

/// <summary>Lê e grava o settings.json. Um arquivo estragado vira "settings.json.estragado" e o app começa do zero.</summary>
public sealed class SettingsStore(string path)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Path { get; } = path;

    /// <summary>Se o arquivo não existia (primeira vez que o app abre).</summary>
    public bool FirstRun { get; private set; }

    public Settings Load()
    {
        if (!File.Exists(Path))
        {
            FirstRun = true;
            return new Settings();
        }

        try
        {
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path), Json) ?? new Settings();
            // O desserializador cria o dicionário sem ignorar maiúsculas; os caminhos do Windows ignoram.
            var channels = new Dictionary<string, ChannelRecord>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in settings.Channels)
            {
                channels.TryAdd(key, value);
            }

            settings.Channels = channels;
            settings.CheckIntervalHours = Math.Clamp(settings.CheckIntervalHours, 1, 24);
            return settings;
        }
        catch (Exception e) when (e is JsonException or IOException or NotSupportedException)
        {
            try
            {
                File.Move(Path, Path + ".estragado", overwrite: true);
            }
            catch (IOException)
            {
            }

            FirstRun = true;
            return new Settings();
        }
    }

    public void Save(Settings settings)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var temp = Path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Json));
        File.Move(temp, Path, overwrite: true);
    }
}
