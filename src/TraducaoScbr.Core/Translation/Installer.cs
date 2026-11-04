using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace TraducaoScbr.Core.Translation;

/// <summary>Um erro ao mexer na pasta do jogo, com a mensagem pronta para a tela.</summary>
public sealed class InstallException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>O que há de tradução num canal.</summary>
/// <param name="Installed">Se o global.ini da tradução existe.</param>
/// <param name="Sha256">O sha256 do global.ini instalado.</param>
/// <param name="CfgLanguage">O idioma escolhido no user.cfg (g_language), se houver.</param>
public sealed record ChannelInspection(bool Installed, string? Sha256, string? CfgLanguage)
{
    public static readonly ChannelInspection Empty = new(false, null, null);

    public bool LanguageActive => string.Equals(CfgLanguage, Installer.Language, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Instala e remove a tradução de um canal, como os .cmd da equipe: o arquivo vai em
/// <c>&lt;canal&gt;\data\Localization\portuguese_(brazil)\global.ini</c> e o user.cfg do canal ganha
/// <c>g_language = portuguese_(brazil)</c>. Antes de trocar um arquivo, fica uma cópia .bak ao lado.
/// </summary>
public static class Installer
{
    public const string Language = "portuguese_(brazil)";
    public const string IniName = "global.ini";

    private static readonly ConcurrentDictionary<string, (long Size, DateTime Modified, string Sha)> HashCache =
        new(StringComparer.OrdinalIgnoreCase);

    public static string LanguageFolder(string channelFolder) =>
        Path.Combine(channelFolder, "data", "Localization", Language);

    public static string IniPath(string channelFolder) => Path.Combine(LanguageFolder(channelFolder), IniName);

    /// <summary>O user.cfg do canal com o nome como está no disco (no LIVE ele costuma ser "User.cfg").</summary>
    public static string? FindUserCfg(string channelFolder)
    {
        try
        {
            return Directory.Exists(channelFolder)
                ? Directory.EnumerateFiles(channelFolder, UserCfg.FileName)
                    .FirstOrDefault(f => string.Equals(Path.GetFileName(f), UserCfg.FileName, StringComparison.OrdinalIgnoreCase))
                : null;
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

    public static ChannelInspection Inspect(string channelFolder)
    {
        string? language = null;
        try
        {
            if (FindUserCfg(channelFolder) is { } cfg)
            {
                language = UserCfg.GetLanguage(TextFile.Read(cfg).Content);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        var ini = IniPath(channelFolder);
        var sha = TryHash(ini);
        return new ChannelInspection(sha is not null, sha, language);
    }

    /// <summary>
    /// Copia o global.ini já conferido (com o sha256 <paramref name="sha256"/>) para o canal e escolhe o português
    /// no user.cfg. Se o canal já tem esse arquivo, só o user.cfg é conferido.
    /// </summary>
    public static void Install(string channelFolder, string verifiedIni, string sha256)
    {
        Guard(channelFolder, () =>
        {
            if (!Directory.Exists(channelFolder))
            {
                throw new InstallException("A pasta do canal não existe mais.");
            }

            var folder = LanguageFolder(channelFolder);
            Directory.CreateDirectory(folder);
            var target = Path.Combine(folder, IniName);

            if (!string.Equals(TryHash(target), sha256, StringComparison.OrdinalIgnoreCase))
            {
                var temp = target + ".novo";
                File.Copy(verifiedIni, temp, overwrite: true);
                try
                {
                    if (File.Exists(target))
                    {
                        Backup(target);
                    }

                    File.Move(temp, target, overwrite: true);
                }
                finally
                {
                    TextFile.TryDelete(temp);
                }
            }

            SetLanguage(channelFolder);
        });
    }

    /// <summary>
    /// Tira a tradução do canal: apaga a pasta portuguese_(brazil) e a linha do user.cfg. As pastas data e
    /// Localization saem só se ficarem vazias.
    /// </summary>
    public static void Remove(string channelFolder)
    {
        Guard(channelFolder, () =>
        {
            var folder = LanguageFolder(channelFolder);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }

            DeleteIfEmpty(Path.GetDirectoryName(folder));
            DeleteIfEmpty(Path.GetDirectoryName(Path.GetDirectoryName(folder)));

            if (FindUserCfg(channelFolder) is not { } cfg)
            {
                return;
            }

            var file = TextFile.Read(cfg);
            var updated = UserCfg.RemoveLanguage(file.Content, Language);
            if (updated == file.Content)
            {
                return;
            }

            Backup(cfg);
            if (updated.Length == 0)
            {
                // Sobrou só a linha do idioma: o arquivo sai, como antes da instalação.
                File.SetAttributes(cfg, FileAttributes.Normal);
                File.Delete(cfg);
            }
            else
            {
                TextFile.WriteAtomic(cfg, file.ToBytes(updated));
            }
        });
    }

    public static string Sha256Of(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 81920);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static void SetLanguage(string channelFolder)
    {
        var cfg = FindUserCfg(channelFolder);
        if (cfg is null)
        {
            TextFile.WriteAtomic(Path.Combine(channelFolder, UserCfg.FileName), System.Text.Encoding.UTF8.GetBytes(UserCfg.LanguageLine(Language) + "\r\n"));
            return;
        }

        var file = TextFile.Read(cfg);
        var updated = UserCfg.SetLanguage(file.Content, Language);
        if (updated == file.Content)
        {
            return;
        }

        Backup(cfg);
        TextFile.WriteAtomic(cfg, file.ToBytes(updated));
    }

    // A cópia .bak ao lado, sempre gravável (a cópia de um arquivo somente leitura também nasce assim).
    private static void Backup(string path)
    {
        var backup = path + ".bak";
        if (File.Exists(backup))
        {
            File.SetAttributes(backup, FileAttributes.Normal);
        }

        File.Copy(path, backup, overwrite: true);
        File.SetAttributes(backup, FileAttributes.Normal);
    }

    // O sha256 do arquivo, guardado pelo tamanho e pela data, para não ler 11 MB de cada canal a cada conferência.
    private static string? TryHash(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return null;
            }

            if (HashCache.TryGetValue(path, out var cached) && cached.Size == info.Length && cached.Modified == info.LastWriteTimeUtc)
            {
                return cached.Sha;
            }

            var sha = Sha256Of(path);
            HashCache[path] = (info.Length, info.LastWriteTimeUtc, sha);
            return sha;
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

    private static void DeleteIfEmpty(string? folder)
    {
        try
        {
            if (folder is not null && Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
            {
                Directory.Delete(folder);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void Guard(string channelFolder, Action action)
    {
        try
        {
            action();
        }
        catch (InstallException)
        {
            throw;
        }
        catch (UnauthorizedAccessException e)
        {
            throw new InstallException($"O Windows não deixou mexer na pasta do jogo ({channelFolder}). Confira se ela não está como somente leitura.", e);
        }
        catch (IOException e) when (e.HResult is unchecked((int)0x80070020) or unchecked((int)0x80070021))
        {
            throw new InstallException("Um arquivo da tradução está em uso. Feche o jogo e tente de novo.", e);
        }
        catch (IOException e)
        {
            throw new InstallException($"Não deu para gravar na pasta do jogo: {e.Message}", e);
        }
    }
}
