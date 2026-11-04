using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TraducaoScbr.Core.Game;

/// <summary>
/// O log do RSI Launcher (<c>%APPDATA%\rsilauncher\logs\log.log</c>) diz de onde cada canal foi aberto:
/// <c>"[Launcher::launch] Launching Star Citizen LIVE from (C:\\Program Files\\...\\StarCitizen\\LIVE)"</c>.
/// É o jeito de achar o jogo instalado fora das pastas comuns. As barras vêm escapadas, porque cada linha é JSON.
/// </summary>
public static partial class LauncherLog
{
    private const string Marker = "Launching Star Citizen ";

    // O ".+" é guloso de propósito: o caminho pode ter parênteses, como "Program Files (x86)".
    [GeneratedRegex(@"Launching Star Citizen (?<canal>[A-Za-z0-9_-]+) from \((?<pasta>.+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex LaunchLine();

    /// <summary>Os logs que o launcher costuma deixar, do mais novo para o mais velho.</summary>
    public static IReadOnlyList<string> DefaultFiles()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(appData))
        {
            return [];
        }

        var logs = Path.Combine(appData, "rsilauncher", "logs");
        return [Path.Combine(logs, "log.log"), Path.Combine(logs, "log.old.log")];
    }

    /// <summary>As pastas de canal citadas no texto do log, sem repetir, da abertura mais recente para a mais velha.</summary>
    public static IReadOnlyList<string> ParseChannelFolders(string text)
    {
        var folders = new List<string>();
        foreach (var line in text.Split('\n'))
        {
            if (!line.Contains(Marker, StringComparison.Ordinal))
            {
                continue;
            }

            var match = LaunchLine().Match(line);
            if (!match.Success)
            {
                continue;
            }

            var folder = Unescape(match.Groups["pasta"].Value.Trim());
            if (folder.Length > 0)
            {
                folders.Add(folder);
            }
        }

        folders.Reverse();
        return folders.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Lê um log do launcher sem travar o arquivo, que o launcher mantém aberto.</summary>
    public static IReadOnlyList<string> ReadChannelFolders(string logFile)
    {
        try
        {
            if (!File.Exists(logFile))
            {
                return [];
            }

            using var stream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return ParseChannelFolders(reader.ReadToEnd());
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    // Nas linhas em JSON, "C:\\Program Files" vira "C:\Program Files". Um caminho que não é JSON válido (com
    // barras simples, de um launcher antigo) fica como veio.
    private static string Unescape(string raw)
    {
        if (!raw.Contains(@"\\", StringComparison.Ordinal))
        {
            return raw;
        }

        try
        {
            return JsonSerializer.Deserialize<string>("\"" + raw + "\"") ?? raw;
        }
        catch (JsonException)
        {
            return raw;
        }
    }
}
