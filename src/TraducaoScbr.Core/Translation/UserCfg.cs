using System.Text.RegularExpressions;

namespace TraducaoScbr.Core.Translation;

/// <summary>
/// Edita o user.cfg de um canal: a linha <c>g_language = portuguese_(brazil)</c> escolhe a tradução. As outras
/// linhas ficam como estavam, com as mesmas quebras de linha.
/// </summary>
public static partial class UserCfg
{
    public const string FileName = "user.cfg";
    public const string LanguageCvar = "g_language";

    // O nome da variável vai até o primeiro caractere que não é de nome: "g_languageAudio" é outra variável
    // (os .cmd da equipe trocam essa linha por engano, porque procuram "^g_language").
    [GeneratedRegex(@"^\s*(?<name>[A-Za-z_][A-Za-z0-9_.]*)\s*(?:=|\s)\s*(?<value>.*?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex CvarLine();

    /// <summary>O idioma escolhido no arquivo (a última linha g_language vale), ou null quando não há.</summary>
    public static string? GetLanguage(string content)
    {
        string? last = null;
        foreach (var line in Split(content).Lines)
        {
            if (TryLanguage(line, out var value))
            {
                last = value;
            }
        }

        return last;
    }

    /// <summary>Escolhe o idioma: troca a primeira linha g_language, tira as repetidas, ou põe uma no fim.</summary>
    public static string SetLanguage(string content, string language)
    {
        var (lines, newline, trailing) = Split(content);
        var result = new List<string>(lines.Count + 1);
        var placed = false;
        foreach (var line in lines)
        {
            if (!TryLanguage(line, out var value))
            {
                result.Add(line);
                continue;
            }

            if (!placed)
            {
                // A linha que já escolhe o idioma fica como está, para não mexer no arquivo à toa.
                result.Add(string.Equals(value, language, StringComparison.OrdinalIgnoreCase) ? line : LanguageLine(language));
                placed = true;
            }
        }

        if (!placed)
        {
            result.Add(LanguageLine(language));
            trailing = true;
        }

        return Join(result, newline, trailing);
    }

    /// <summary>
    /// Tira as linhas que escolhem este idioma. Uma linha com outro idioma fica. Devolve "" quando o arquivo fica
    /// só com linhas em branco.
    /// </summary>
    public static string RemoveLanguage(string content, string language)
    {
        var (lines, newline, trailing) = Split(content);
        var kept = lines
            .Where(line => !(TryLanguage(line, out var value) && string.Equals(value, language, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (kept.Count == lines.Count)
        {
            return content;
        }

        return kept.All(string.IsNullOrWhiteSpace) ? "" : Join(kept, newline, trailing);
    }

    public static string LanguageLine(string language) => $"{LanguageCvar} = {language}";

    private static bool TryLanguage(string line, out string value)
    {
        var match = CvarLine().Match(line);
        if (match.Success && string.Equals(match.Groups["name"].Value, LanguageCvar, StringComparison.OrdinalIgnoreCase))
        {
            value = match.Groups["value"].Value.Trim().Trim('"').Trim();
            return true;
        }

        value = "";
        return false;
    }

    private static (List<string> Lines, string Newline, bool Trailing) Split(string content)
    {
        var newline = content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : content.Contains('\n') ? "\n" : "\r\n";
        if (content.Length == 0)
        {
            return ([], newline, true);
        }

        var trailing = content.EndsWith('\n');
        var body = trailing ? content[..^1] : content;
        var lines = body.Split('\n').Select(l => l.EndsWith('\r') ? l[..^1] : l).ToList();
        return (lines, newline, trailing);
    }

    private static string Join(List<string> lines, string newline, bool trailing) =>
        lines.Count == 0 ? "" : string.Join(newline, lines) + (trailing ? newline : "");
}
