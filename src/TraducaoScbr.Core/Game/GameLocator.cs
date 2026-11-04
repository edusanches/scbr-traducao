namespace TraducaoScbr.Core.Game;

/// <summary>Onde procurar o jogo.</summary>
/// <param name="DriveRoots">As raízes dos discos (C:\, D:\...), para as pastas comuns.</param>
/// <param name="LauncherLogs">Os logs do RSI Launcher.</param>
/// <param name="ExtraFolders">As pastas que a pessoa escolheu.</param>
/// <param name="AutoDetect">Falso para olhar só as pastas escolhidas (usado nos testes do app).</param>
public sealed record LocatorSources(
    IReadOnlyList<string> DriveRoots,
    IReadOnlyList<string> LauncherLogs,
    IReadOnlyList<string> ExtraFolders,
    bool AutoDetect = true);

/// <summary>
/// Acha os canais instalados. Cada canal é uma pasta com o Data.p4k (ou o build_manifest.id) dentro da pasta
/// StarCitizen do launcher: <c>C:\Program Files\Roberts Space Industries\StarCitizen\LIVE</c>.
/// </summary>
public static class GameLocator
{
    /// <summary>Os canais conhecidos, na ordem em que aparecem na tela. Outros nomes vêm depois, em ordem alfabética.</summary>
    public static readonly string[] KnownChannels = ["LIVE", "PTU", "EPTU", "HOTFIX", "TECH-PREVIEW"];

    /// <summary>
    /// As bibliotecas comuns, a partir da raiz de cada disco: as que os .cmd da equipe de tradução procuram e
    /// mais duas pastas de jogos.
    /// </summary>
    public static readonly string[] CommonLibraries =
    [
        @"Program Files\Roberts Space Industries\StarCitizen",
        @"Program Files (x86)\Roberts Space Industries\StarCitizen",
        @"Roberts Space Industries\StarCitizen",
        @"StarCitizen",
        @"Star Citizen\StarCitizen",
        @"Star Citizen PTU\StarCitizen",
        @"Games\Roberts Space Industries\StarCitizen",
        @"Games\StarCitizen",
    ];

    /// <summary>Os discos fixos prontos e os logs do launcher deste PC.</summary>
    public static LocatorSources DefaultSources(IReadOnlyList<string> extraFolders)
    {
        var drives = new List<string>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType == DriveType.Fixed && drive.IsReady)
                {
                    drives.Add(drive.RootDirectory.FullName);
                }
            }
            catch (IOException)
            {
                // Disco que sumiu no meio da conta.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return new LocatorSources(drives, LauncherLog.DefaultFiles(), extraFolders);
    }

    public static IReadOnlyList<GameChannel> Find(LocatorSources sources)
    {
        var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void Add(string folder)
        {
            if (Normalize(folder) is { } path)
            {
                found.TryAdd(path, path);
            }
        }

        foreach (var extra in sources.ExtraFolders)
        {
            foreach (var channel in ResolveFolder(extra))
            {
                Add(channel);
            }
        }

        if (sources.AutoDetect)
        {
            foreach (var log in sources.LauncherLogs)
            {
                foreach (var folder in LauncherLog.ReadChannelFolders(log))
                {
                    if (!IsChannelFolder(folder))
                    {
                        continue;
                    }

                    Add(folder);
                    // Os outros canais costumam ficar na mesma biblioteca.
                    if (Path.GetDirectoryName(folder) is { } library)
                    {
                        foreach (var sibling in ChannelsInLibrary(library))
                        {
                            Add(sibling);
                        }
                    }
                }
            }

            foreach (var root in sources.DriveRoots)
            {
                foreach (var library in CommonLibraries)
                {
                    foreach (var channel in ChannelsInLibrary(Path.Combine(root, library)))
                    {
                        Add(channel);
                    }
                }
            }
        }

        return found.Values
            .Select(path => new GameChannel(ChannelName(path), path, BuildManifest.Read(path)))
            .OrderBy(c => ChannelOrder(c.Name))
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Os canais de uma pasta escolhida pela pessoa. Vale a pasta do canal (LIVE), uma pasta dentro dela (data,
    /// como nos .cmd da equipe), a biblioteca (StarCitizen) ou a pasta de cima (Roberts Space Industries).
    /// </summary>
    public static IReadOnlyList<string> ResolveFolder(string folder)
    {
        if (Normalize(folder) is not { } dir || !Directory.Exists(dir))
        {
            return [];
        }

        var probe = dir;
        for (var depth = 0; probe is not null && depth < 4; depth++)
        {
            if (IsChannelFolder(probe))
            {
                return [probe];
            }

            probe = Path.GetDirectoryName(probe);
        }

        var channels = ChannelsInLibrary(dir);
        return channels.Count > 0 ? channels : ChannelsInLibrary(Path.Combine(dir, "StarCitizen"));
    }

    public static IReadOnlyList<string> ChannelsInLibrary(string library)
    {
        try
        {
            if (!Directory.Exists(library))
            {
                return [];
            }

            return Directory.EnumerateDirectories(library)
                .Where(IsChannelFolder)
                .Select(d => Normalize(d) ?? d)
                .ToList();
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

    public static bool IsChannelFolder(string folder) =>
        File.Exists(Path.Combine(folder, "Data.p4k"))
        || File.Exists(Path.Combine(folder, BuildManifest.FileName))
        || File.Exists(Path.Combine(folder, "Bin64", "StarCitizen.exe"));

    /// <summary>O caminho completo, sem a barra do fim, ou null quando não é um caminho.</summary>
    public static string? Normalize(string folder)
    {
        var trimmed = folder.Trim().Trim('"');
        if (trimmed.Length == 0)
        {
            return null;
        }

        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(trimmed));
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    public static string ChannelName(string channelFolder) =>
        Path.GetFileName(Path.TrimEndingDirectorySeparator(channelFolder)).ToUpperInvariant();

    private static int ChannelOrder(string name)
    {
        var index = Array.IndexOf(KnownChannels, name);
        return index < 0 ? KnownChannels.Length : index;
    }
}
