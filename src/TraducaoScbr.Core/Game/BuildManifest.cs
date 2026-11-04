using System.Globalization;
using System.Text.Json;

namespace TraducaoScbr.Core.Game;

/// <summary>
/// Lê o build_manifest.id que o launcher deixa na pasta de cada canal:
/// <c>{ "Data": { "Branch": "sc-alpha-4.10.2", "RequestedP4ChangeNum": "12913470", ... } }</c>.
/// O build é o RequestedP4ChangeNum. O Branch não serve para a versão: no PTU 4.10.3 ele dizia sc-alpha-4.10.0.
/// </summary>
public static class BuildManifest
{
    public const string FileName = "build_manifest.id";

    private static readonly JsonDocumentOptions Options = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public static GameBuild? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json, Options);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var data = root.TryGetProperty("Data", out var inner) && inner.ValueKind == JsonValueKind.Object ? inner : root;
            var change = ReadLong(data, "RequestedP4ChangeNum");
            if (change is not > 0)
            {
                return null;
            }

            return new GameBuild(
                change.Value,
                ReadString(data, "Branch"),
                ReadString(data, "Version"),
                ReadString(data, "BuildDateStamp"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>O build do canal, ou null quando o arquivo falta ou não se entende.</summary>
    public static GameBuild? Read(string channelDirectory)
    {
        var path = Path.Combine(channelDirectory, FileName);
        try
        {
            return File.Exists(path) ? Parse(File.ReadAllText(path)) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static long? ReadLong(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var n) => n,
            JsonValueKind.String when long.TryParse(value.GetString()?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n) => n,
            _ => null,
        };
    }

    private static string? ReadString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } s
            ? s
            : null;
}
