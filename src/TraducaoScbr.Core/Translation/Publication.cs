using System.Globalization;
using System.Text.Json;

namespace TraducaoScbr.Core.Translation;

/// <summary>
/// A tradução publicada pela ferramenta do site, como a API devolve em <c>GET /api/traducao/atual</c>:
/// <c>{ versao, canal, build, publicadaEm, tamanho, sha256, arquivo, pagina }</c>. O canal e o build podem faltar
/// (null): sem o build, o app não compara com o jogo.
/// </summary>
public sealed record Publication(
    string Version,
    string? Channel,
    long? Build,
    DateTimeOffset? PublishedAt,
    long Size,
    string Sha256,
    Uri File,
    Uri? Page)
{
    /// <summary>O maior arquivo aceito. O global.ini tem ~11 MB.</summary>
    public const long MaxSize = 64L * 1024 * 1024;

    /// <summary>"4.10.3 PTU".</summary>
    public string Label => $"{Version} {Channel}".Trim();

    /// <summary>"4.10.3 PTU (build 13055118)", ou só "4.10.3 PTU" sem o build.</summary>
    public string Description => Build is { } build ? $"{Label} (build {build})" : Label;

    public PublicationRecord ToRecord() => new(Version, Channel, Build, Sha256, PublishedAt);

    /// <summary>Lê a resposta da API. As URLs relativas valem a partir do endereço da API.</summary>
    /// <exception cref="FormatException">Quando falta um campo ou um valor não faz sentido.</exception>
    public static Publication Parse(string json, Uri apiUri)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw new FormatException("A resposta não é JSON.", e);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("A resposta não é um objeto.");
            }

            var version = RequiredString(root, "versao");
            var channel = OptionalString(root, "canal");
            var build = OptionalLong(root, "build");
            var size = OptionalLong(root, "tamanho") ?? throw new FormatException("Falta o número \"tamanho\".");
            if (build is <= 0)
            {
                throw new FormatException("O build precisa ser maior que zero.");
            }

            if (size <= 0 || size > MaxSize)
            {
                throw new FormatException("O tamanho do arquivo está fora do esperado.");
            }

            var sha = RequiredString(root, "sha256").ToLowerInvariant();
            if (sha.Length != 64 || !sha.All(char.IsAsciiHexDigitLower))
            {
                throw new FormatException("O sha256 não tem 64 caracteres hexadecimais.");
            }

            var file = ResolveUrl(apiUri, RequiredString(root, "arquivo"))
                ?? throw new FormatException("O endereço do arquivo não é uma URL http(s).");
            var page = OptionalString(root, "pagina") is { } p ? ResolveUrl(apiUri, p) : null;

            DateTimeOffset? publishedAt = null;
            if (OptionalString(root, "publicadaEm") is { } when
                && DateTimeOffset.TryParse(when, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            {
                publishedAt = parsed;
            }

            return new Publication(version, channel, build, publishedAt, size, sha, file, page);
        }
    }

    private static Uri? ResolveUrl(Uri apiUri, string value)
    {
        if (!Uri.TryCreate(apiUri, value, out var uri))
        {
            return null;
        }

        return uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && IsLocal(uri)) ? uri : null;
    }

    // http só no mock local; o site é sempre https.
    private static bool IsLocal(Uri uri) => uri.IsLoopback || uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);

    private static string RequiredString(JsonElement root, string name) =>
        OptionalString(root, name) ?? throw new FormatException($"Falta o campo \"{name}\".");

    private static string? OptionalString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString()?.Trim() is { Length: > 0 } s
            ? s
            : null;

    // Um número, em número ou em texto. Ausente ou null vira null; qualquer outra coisa é erro.
    private static long? OptionalLong(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var n))
        {
            return n;
        }

        if (value.ValueKind == JsonValueKind.String
            && long.TryParse(value.GetString()?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var s))
        {
            return s;
        }

        throw new FormatException($"O campo \"{name}\" não é um número.");
    }
}

/// <summary>Uma publicação guardada nas configurações: a última vista e a instalada em cada canal.</summary>
public sealed record PublicationRecord(string Version, string? Channel, long? Build, string Sha256, DateTimeOffset? PublishedAt)
{
    public string Label => $"{Version} {Channel}".Trim();

    public string Description => Build is { } build ? $"{Label} (build {build})" : Label;
}
